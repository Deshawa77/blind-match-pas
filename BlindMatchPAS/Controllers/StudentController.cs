using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.ViewModels;
using BlindMatchPAS.Constants;
using BlindMatchPAS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Controllers
{
    [Authorize(Roles = ApplicationRoles.Student)]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStudentService _studentService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemSettingsService _settingsService;
        private readonly IAuditService _auditService;

        public StudentController(
            ApplicationDbContext context,
            IStudentService studentService,
            UserManager<ApplicationUser> userManager,
            ISystemSettingsService settingsService,
            IAuditService auditService)
        {
            _context = context;
            _studentService = studentService;
            _userManager = userManager;
            _settingsService = settingsService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var proposals = await GetProposalSummariesAsync(user.Id);
            var settings = await _settingsService.GetSettingsAsync();
            var group = await GetProjectGroupForStudentAsync(user.Id);
            var model = new StudentDashboardViewModel
            {
                StudentName = user.FullName,
                CanSubmitNow = await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow),
                SubmissionOpensAtUtc = settings.ProposalSubmissionOpensAtUtc,
                SubmissionClosesAtUtc = settings.ProposalSubmissionClosesAtUtc,
                ProposalCount = proposals.Count,
                PendingCount = proposals.Count(proposal => proposal.Status == ProposalStatuses.Pending),
                UnderReviewCount = proposals.Count(proposal => proposal.Status == ProposalStatuses.UnderReview),
                MatchedCount = proposals.Count(proposal => proposal.Status == ProposalStatuses.Matched),
                WithdrawnCount = proposals.Count(proposal => proposal.Status == ProposalStatuses.Withdrawn),
                HasProjectGroup = group != null,
                IsProjectGroupLead = group?.LeadStudentId == user.Id,
                ProjectGroupName = group?.Name,
                ProjectGroupMemberCount = group?.Members.Count ?? 0,
                RecentProposals = proposals.Take(4).ToList()
            };

            return View(model);
        }

        // View all proposals created by the logged-in student
        public async Task<IActionResult> MyProposals()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var proposals = await GetProposalSummariesAsync(user.Id);
            return View(proposals);
        }

        [HttpGet]
        public async Task<IActionResult> MyGroup()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var group = await GetProjectGroupForStudentAsync(user.Id);
            var model = await BuildGroupWorkspaceAsync(user, group);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup([Bind(Prefix = "Editor")] ProjectGroupEditorViewModel editor)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var existingGroup = await GetProjectGroupForStudentAsync(user.Id);
            if (existingGroup != null)
            {
                TempData["ErrorMessage"] = "You are already assigned to a project group.";
                return RedirectToAction(nameof(MyGroup));
            }

            await ValidateGroupEditorAsync(editor, user.Id, null);
            if (!ModelState.IsValid)
            {
                var model = await BuildGroupWorkspaceAsync(user, null, editor);
                return View(nameof(MyGroup), model);
            }

            var selectedMemberIds = editor.SelectedMemberIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var group = new ProjectGroup
            {
                Name = NormalizeGroupName(editor.GroupName),
                LeadStudentId = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            group.Members.Add(new ProjectGroupMember
            {
                StudentId = user.Id,
                IsLead = true,
                JoinedAtUtc = DateTime.UtcNow
            });

            foreach (var memberId in selectedMemberIds)
            {
                group.Members.Add(new ProjectGroupMember
                {
                    StudentId = memberId,
                    IsLead = false,
                    JoinedAtUtc = DateTime.UtcNow
                });
            }

            _context.ProjectGroups.Add(group);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "ProjectGroupCreated",
                nameof(ProjectGroup),
                group.Id.ToString(),
                $"Project group '{group.Name}' was created.",
                user.Id,
                user.FullName,
                metadata: new
                {
                    group.Name,
                    MemberCount = group.Members.Count
                });

            TempData["SuccessMessage"] = "Project group created successfully.";
            return RedirectToAction(nameof(MyGroup));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGroup([Bind(Prefix = "Editor")] ProjectGroupEditorViewModel editor)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!editor.ProjectGroupId.HasValue)
            {
                return BadRequest();
            }

            var group = await _context.ProjectGroups
                .Include(projectGroup => projectGroup.Members)
                .Include(projectGroup => projectGroup.ProjectProposals)
                .FirstOrDefaultAsync(projectGroup => projectGroup.Id == editor.ProjectGroupId.Value && projectGroup.LeadStudentId == user.Id);

            if (group == null)
            {
                return NotFound();
            }

            if (HasLockedProposalActivity(group))
            {
                TempData["ErrorMessage"] = "This group cannot be changed while it has active proposal workflow history.";
                return RedirectToAction(nameof(MyGroup));
            }

            await ValidateGroupEditorAsync(editor, user.Id, group.Id);
            if (!ModelState.IsValid)
            {
                var reloadedGroup = await GetProjectGroupForStudentAsync(user.Id);
                var model = await BuildGroupWorkspaceAsync(user, reloadedGroup, editor);
                return View(nameof(MyGroup), model);
            }

            var selectedMemberIds = editor.SelectedMemberIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            group.Name = NormalizeGroupName(editor.GroupName);
            group.UpdatedAtUtc = DateTime.UtcNow;

            var currentNonLeadMembers = group.Members.Where(member => !member.IsLead).ToList();
            foreach (var member in currentNonLeadMembers.Where(member => !selectedMemberIds.Contains(member.StudentId)).ToList())
            {
                _context.ProjectGroupMembers.Remove(member);
            }

            var existingMemberIds = group.Members.Select(member => member.StudentId).ToHashSet(StringComparer.Ordinal);
            foreach (var memberId in selectedMemberIds.Where(memberId => !existingMemberIds.Contains(memberId)))
            {
                group.Members.Add(new ProjectGroupMember
                {
                    StudentId = memberId,
                    IsLead = false,
                    JoinedAtUtc = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "ProjectGroupUpdated",
                nameof(ProjectGroup),
                group.Id.ToString(),
                $"Project group '{group.Name}' was updated.",
                user.Id,
                user.FullName,
                metadata: new
                {
                    group.Name,
                    MemberCount = group.Members.Count
                });

            TempData["SuccessMessage"] = "Project group updated successfully.";
            return RedirectToAction(nameof(MyGroup));
        }

        // Show create form
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow))
            {
                TempData["ErrorMessage"] = "Proposal submissions are currently closed.";
                return RedirectToAction(nameof(Dashboard));
            }

            var model = new ProjectProposalViewModel();
            await PopulateProposalFormAsync(user, model);
            return View(model);
        }

        // Submit proposal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectProposalViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (!await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow))
            {
                ModelState.AddModelError(string.Empty, "Proposal submissions are currently closed.");
            }

            await ValidateProposalOwnershipAsync(user, model);

            if (!ModelState.IsValid)
            {
                await PopulateProposalFormAsync(user, model);
                return View(model);
            }

            if (!await ResearchAreaExistsAsync(model.ResearchAreaId))
            {
                ModelState.AddModelError(nameof(model.ResearchAreaId), "Please select a valid research area.");
                await PopulateProposalFormAsync(user, model);
                return View(model);
            }

            var created = await _studentService.CreateProposalAsync(user.Id, model);

            if (!created)
            {
                ModelState.AddModelError(string.Empty, "The proposal could not be saved. Please verify the selected research area and ownership option.");
                await PopulateProposalFormAsync(user, model);
                return View(model);
            }

            TempData["SuccessMessage"] = "Proposal submitted successfully.";
            return RedirectToAction(nameof(MyProposals));
        }

        // Show edit form
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var proposal = await _context.ProjectProposals
                .Include(projectProposal => projectProposal.ProjectGroup)
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

            if (proposal == null)
            {
                return NotFound();
            }

            if (proposal.IsMatched || proposal.Status == ProposalStatuses.Matched || proposal.Status == ProposalStatuses.Withdrawn)
            {
                TempData["ErrorMessage"] = "You cannot edit a matched or withdrawn proposal.";
                return RedirectToAction(nameof(MyProposals));
            }

            if (!await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow))
            {
                TempData["ErrorMessage"] = "Proposal editing is currently closed.";
                return RedirectToAction(nameof(MyProposals));
            }

            var model = new ProjectProposalViewModel
            {
                Id = proposal.Id,
                Title = proposal.Title,
                Abstract = proposal.Abstract,
                TechnicalStack = proposal.TechnicalStack,
                ResearchAreaId = proposal.ResearchAreaId,
                OwnershipType = proposal.ProjectGroupId.HasValue ? ProposalOwnershipTypes.Group : ProposalOwnershipTypes.Individual,
                ProjectGroupId = proposal.ProjectGroupId,
                SelectedGroupName = proposal.ProjectGroup?.Name
            };

            await PopulateProposalFormAsync(user, model);
            return View(model);
        }

        // Update proposal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProjectProposalViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow))
            {
                ModelState.AddModelError(string.Empty, "Proposal editing is currently closed.");
            }

            var proposal = await _context.ProjectProposals
                .Include(projectProposal => projectProposal.ProjectGroup)
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

            if (proposal == null)
            {
                return NotFound();
            }

            if (proposal.IsMatched || proposal.Status == ProposalStatuses.Matched || proposal.Status == ProposalStatuses.Withdrawn)
            {
                TempData["ErrorMessage"] = "You cannot edit a matched or withdrawn proposal.";
                return RedirectToAction(nameof(MyProposals));
            }

            var expectedOwnershipType = proposal.ProjectGroupId.HasValue ? ProposalOwnershipTypes.Group : ProposalOwnershipTypes.Individual;
            if (!string.Equals(model.OwnershipType, expectedOwnershipType, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.OwnershipType), "Proposal ownership cannot be changed after submission.");
            }

            if (proposal.ProjectGroupId != model.ProjectGroupId)
            {
                ModelState.AddModelError(nameof(model.ProjectGroupId), "Proposal ownership cannot be changed after submission.");
            }

            if (!ModelState.IsValid)
            {
                model.ProjectGroupId = proposal.ProjectGroupId;
                model.OwnershipType = expectedOwnershipType;
                model.SelectedGroupName = proposal.ProjectGroup?.Name;
                await PopulateProposalFormAsync(user, model);
                return View(model);
            }

            if (!await ResearchAreaExistsAsync(model.ResearchAreaId))
            {
                ModelState.AddModelError(nameof(model.ResearchAreaId), "Please select a valid research area.");
                model.ProjectGroupId = proposal.ProjectGroupId;
                model.OwnershipType = expectedOwnershipType;
                model.SelectedGroupName = proposal.ProjectGroup?.Name;
                await PopulateProposalFormAsync(user, model);
                return View(model);
            }

            proposal.Title = model.Title;
            proposal.Abstract = model.Abstract;
            proposal.TechnicalStack = model.TechnicalStack;
            proposal.ResearchAreaId = model.ResearchAreaId;
            proposal.UpdatedAt = DateTime.UtcNow;

            _context.ProjectProposals.Update(proposal);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "ProposalUpdated",
                nameof(ProjectProposal),
                proposal.Id.ToString(),
                $"Project proposal '{proposal.Title}' was updated.",
                user.Id,
                user.FullName,
                metadata: new
                {
                    proposal.ResearchAreaId,
                    proposal.Status,
                    proposal.ProjectGroupId
                });

            TempData["SuccessMessage"] = "Proposal updated successfully.";
            return RedirectToAction(nameof(MyProposals));
        }

        // Withdraw proposal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var proposal = await _context.ProjectProposals
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

            if (proposal == null)
            {
                return NotFound();
            }

            if (proposal.IsMatched || proposal.Status == ProposalStatuses.Matched)
            {
                TempData["ErrorMessage"] = "You cannot withdraw a matched proposal.";
                return RedirectToAction(nameof(MyProposals));
            }

            if (!await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow))
            {
                TempData["ErrorMessage"] = "Proposal withdrawals are currently closed.";
                return RedirectToAction(nameof(MyProposals));
            }

            var withdrawn = await _studentService.WithdrawProposalAsync(id, user.Id);

            if (!withdrawn)
            {
                TempData["ErrorMessage"] = "The proposal could not be withdrawn.";
                return RedirectToAction(nameof(MyProposals));
            }

            TempData["SuccessMessage"] = "Proposal withdrawn successfully.";
            return RedirectToAction(nameof(MyProposals));
        }

        private async Task ValidateProposalOwnershipAsync(ApplicationUser user, ProjectProposalViewModel model)
        {
            if (!ProposalOwnershipTypes.All.Contains(model.OwnershipType))
            {
                ModelState.AddModelError(nameof(model.OwnershipType), "Please choose a valid submission type.");
                return;
            }

            var ledGroups = await GetLedGroupsAsync(user.Id);
            model.CanSubmitAsGroup = ledGroups.Any();

            if (model.OwnershipType == ProposalOwnershipTypes.Individual)
            {
                model.ProjectGroupId = null;
                model.SelectedGroupName = null;
                return;
            }

            if (!model.CanSubmitAsGroup)
            {
                ModelState.AddModelError(nameof(model.OwnershipType), "Only a group lead can submit a group proposal.");
                return;
            }

            if (!model.ProjectGroupId.HasValue)
            {
                ModelState.AddModelError(nameof(model.ProjectGroupId), "Please select the project group that owns this proposal.");
                return;
            }

            var selectedGroup = ledGroups.FirstOrDefault(projectGroup => projectGroup.Id == model.ProjectGroupId.Value);
            if (selectedGroup == null)
            {
                ModelState.AddModelError(nameof(model.ProjectGroupId), "Please select a valid project group.");
                return;
            }

            model.SelectedGroupName = selectedGroup.Name;
        }

        private async Task ValidateGroupEditorAsync(ProjectGroupEditorViewModel editor, string leadStudentId, int? currentGroupId)
        {
            editor.GroupName = NormalizeGroupName(editor.GroupName);

            if (string.IsNullOrWhiteSpace(editor.GroupName))
            {
                ModelState.AddModelError(nameof(editor.GroupName), "Please provide a project group name.");
            }

            var selectedMemberIds = editor.SelectedMemberIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (selectedMemberIds.Contains(leadStudentId, StringComparer.Ordinal))
            {
                ModelState.AddModelError(nameof(editor.SelectedMemberIds), "The group lead is added automatically and should not be selected again.");
            }

            if (selectedMemberIds.Count + 1 > ProjectGroupRules.MaxMembers)
            {
                ModelState.AddModelError(nameof(editor.SelectedMemberIds), $"A project group can contain at most {ProjectGroupRules.MaxMembers} students including the group lead.");
            }

            var availableStudentIds = await _context.Users
                .Where(user => user.RoleType == ApplicationRoles.Student && user.Id != leadStudentId)
                .Select(user => user.Id)
                .ToListAsync();

            var invalidStudentIds = selectedMemberIds
                .Except(availableStudentIds, StringComparer.Ordinal)
                .ToList();

            if (invalidStudentIds.Any())
            {
                ModelState.AddModelError(nameof(editor.SelectedMemberIds), "All selected group members must be valid student accounts.");
            }

            var blockedMembershipIds = await _context.ProjectGroupMembers
                .Where(member => selectedMemberIds.Contains(member.StudentId) && (!currentGroupId.HasValue || member.ProjectGroupId != currentGroupId.Value))
                .Select(member => member.StudentId)
                .ToListAsync();

            if (blockedMembershipIds.Any())
            {
                ModelState.AddModelError(nameof(editor.SelectedMemberIds), "One or more selected students are already assigned to another project group.");
            }
        }

        private async Task<bool> ResearchAreaExistsAsync(int researchAreaId)
        {
            return await _context.ResearchAreas.AnyAsync(researchArea => researchArea.Id == researchAreaId);
        }

        private async Task LoadResearchAreasAsync(int? selectedId = null)
        {
            var researchAreas = await _context.ResearchAreas
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.ResearchAreas = new SelectList(researchAreas, "Id", "Name", selectedId);
        }

        private async Task PopulateProposalFormAsync(ApplicationUser user, ProjectProposalViewModel model)
        {
            await LoadResearchAreasAsync(model.ResearchAreaId);

            var ledGroups = await GetLedGroupsAsync(user.Id);
            model.CanSubmitAsGroup = ledGroups.Any();
            if (!model.CanSubmitAsGroup)
            {
                model.OwnershipType = ProposalOwnershipTypes.Individual;
                model.ProjectGroupId = null;
                model.SelectedGroupName = null;
            }
            else if (model.ProjectGroupId.HasValue)
            {
                model.SelectedGroupName = ledGroups
                    .Where(projectGroup => projectGroup.Id == model.ProjectGroupId.Value)
                    .Select(projectGroup => projectGroup.Name)
                    .FirstOrDefault();
            }

            ViewBag.LedGroups = new SelectList(ledGroups, "Id", "Name", model.ProjectGroupId);
        }

        private async Task<List<ProjectGroup>> GetLedGroupsAsync(string userId)
        {
            return await _context.ProjectGroups
                .Where(projectGroup => projectGroup.LeadStudentId == userId)
                .OrderBy(projectGroup => projectGroup.Name)
                .ToListAsync();
        }

        private async Task<List<ProjectGroupMemberOptionViewModel>> GetAssignableStudentsAsync(string currentUserId, int? currentGroupId, IEnumerable<string>? selectedMemberIds = null)
        {
            var selectedIds = selectedMemberIds?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            var reservedStudentIds = await _context.ProjectGroupMembers
                .Where(member => member.StudentId != currentUserId && (!currentGroupId.HasValue || member.ProjectGroupId != currentGroupId.Value))
                .Select(member => member.StudentId)
                .ToListAsync();

            return await _context.Users
                .Where(user => user.RoleType == ApplicationRoles.Student
                    && user.Id != currentUserId
                    && (!reservedStudentIds.Contains(user.Id) || selectedIds.Contains(user.Id)))
                .OrderBy(user => user.FullName)
                .Select(user => new ProjectGroupMemberOptionViewModel
                {
                    StudentId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    Selected = selectedIds.Contains(user.Id)
                })
                .ToListAsync();
        }

        private async Task<ProjectGroupWorkspaceViewModel> BuildGroupWorkspaceAsync(
            ApplicationUser user,
            ProjectGroup? group,
            ProjectGroupEditorViewModel? editorOverride = null)
        {
            var model = new ProjectGroupWorkspaceViewModel
            {
                StudentName = user.FullName
            };

            if (group == null)
            {
                model.Editor = editorOverride ?? new ProjectGroupEditorViewModel();
                model.AvailableStudents = await GetAssignableStudentsAsync(user.Id, null, model.Editor.SelectedMemberIds);
                return model;
            }

            model.HasGroup = true;
            model.IsLead = group.LeadStudentId == user.Id;
            model.GroupName = group.Name;
            model.LeadName = group.LeadStudent?.FullName ?? "Unavailable";
            model.MemberCount = group.Members.Count;
            model.GroupLockedByProposalActivity = HasLockedProposalActivity(group);
            model.Members = group.Members
                .OrderByDescending(member => member.IsLead)
                .ThenBy(member => member.Student != null ? member.Student.FullName : member.StudentId)
                .Select(member => new ProjectGroupMemberOptionViewModel
                {
                    StudentId = member.StudentId,
                    FullName = member.Student?.FullName ?? "Unavailable",
                    Email = member.Student?.Email ?? string.Empty,
                    IsLead = member.IsLead,
                    Selected = true
                })
                .ToList();

            model.Editor = editorOverride ?? new ProjectGroupEditorViewModel
            {
                ProjectGroupId = group.Id,
                GroupName = group.Name,
                SelectedMemberIds = group.Members
                    .Where(member => !member.IsLead)
                    .Select(member => member.StudentId)
                    .ToList()
            };

            if (model.IsLead)
            {
                model.AvailableStudents = await GetAssignableStudentsAsync(user.Id, group.Id, model.Editor.SelectedMemberIds);
            }

            return model;
        }

        private async Task<ProjectGroup?> GetProjectGroupForStudentAsync(string userId)
        {
            var membership = await _context.ProjectGroupMembers
                .Where(projectGroupMember => projectGroupMember.StudentId == userId)
                .Select(projectGroupMember => projectGroupMember.ProjectGroupId)
                .SingleOrDefaultAsync();

            if (membership == 0)
            {
                return null;
            }

            return await _context.ProjectGroups
                .Include(projectGroup => projectGroup.LeadStudent)
                .Include(projectGroup => projectGroup.Members)
                    .ThenInclude(projectGroupMember => projectGroupMember.Student)
                .Include(projectGroup => projectGroup.ProjectProposals)
                .FirstOrDefaultAsync(projectGroup => projectGroup.Id == membership);
        }

        private Task<List<StudentProposalSummaryViewModel>> GetProposalSummariesAsync(string userId)
        {
            return _context.ProjectProposals
                .Include(projectProposal => projectProposal.ResearchArea)
                .Include(projectProposal => projectProposal.ProjectGroup)
                    .ThenInclude(projectGroup => projectGroup!.Members)
                .Include(projectProposal => projectProposal.MatchRecords)
                    .ThenInclude(matchRecord => matchRecord.Supervisor)
                .Where(projectProposal => projectProposal.StudentId == userId
                    || (projectProposal.ProjectGroup != null
                        && projectProposal.ProjectGroup.Members.Any(projectGroupMember => projectGroupMember.StudentId == userId)))
                .OrderByDescending(projectProposal => projectProposal.CreatedAt)
                .Select(projectProposal => new StudentProposalSummaryViewModel
                {
                    Id = projectProposal.Id,
                    Title = projectProposal.Title,
                    ResearchAreaName = projectProposal.ResearchArea != null ? projectProposal.ResearchArea.Name : "Unassigned",
                    IsGroupProposal = projectProposal.ProjectGroupId != null,
                    OwnershipLabel = projectProposal.ProjectGroup != null
                        ? $"Group submission: {projectProposal.ProjectGroup.Name}"
                        : "Individual submission",
                    Status = projectProposal.Status,
                    IsMatched = projectProposal.IsMatched,
                    IsIdentityRevealed = projectProposal.IsIdentityRevealed,
                    CreatedAt = projectProposal.CreatedAt,
                    UpdatedAt = projectProposal.UpdatedAt,
                    SupervisorFullName = projectProposal.IsIdentityRevealed
                        ? projectProposal.MatchRecords
                            .OrderByDescending(matchRecord => matchRecord.MatchedAt)
                            .Select(matchRecord => matchRecord.Supervisor != null ? matchRecord.Supervisor.FullName : null)
                            .FirstOrDefault()
                        : null,
                    SupervisorEmail = projectProposal.IsIdentityRevealed
                        ? projectProposal.MatchRecords
                            .OrderByDescending(matchRecord => matchRecord.MatchedAt)
                            .Select(matchRecord => matchRecord.Supervisor != null ? matchRecord.Supervisor.Email : null)
                            .FirstOrDefault()
                        : null,
                    MatchedAt = projectProposal.IsIdentityRevealed
                        ? projectProposal.MatchRecords
                            .OrderByDescending(matchRecord => matchRecord.MatchedAt)
                            .Select(matchRecord => (DateTime?)matchRecord.MatchedAt)
                            .FirstOrDefault()
                        : null,
                    CanEdit = projectProposal.StudentId == userId
                        && !projectProposal.IsMatched
                        && projectProposal.Status != ProposalStatuses.Matched
                        && projectProposal.Status != ProposalStatuses.Withdrawn,
                    CanWithdraw = projectProposal.StudentId == userId
                        && !projectProposal.IsMatched
                        && projectProposal.Status != ProposalStatuses.Matched,
                    CanManageOwnership = projectProposal.StudentId == userId
                })
                .ToListAsync();
        }

        private static bool HasLockedProposalActivity(ProjectGroup projectGroup)
        {
            return projectGroup.ProjectProposals.Any(projectProposal => projectProposal.Status != ProposalStatuses.Withdrawn);
        }

        private static string NormalizeGroupName(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
