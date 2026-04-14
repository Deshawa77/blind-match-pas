using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.ViewModels;
using BlindMatchPAS.Constants;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Controllers
{
    [Authorize(Roles = ApplicationRoles.ModuleLeader + "," + ApplicationRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMatchService _matchService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemSettingsService _settingsService;
        private readonly IAuditService _auditService;

        public AdminController(
            ApplicationDbContext context,
            IMatchService matchService,
            UserManager<ApplicationUser> userManager,
            ISystemSettingsService settingsService,
            IAuditService auditService)
        {
            _context = context;
            _matchService = matchService;
            _userManager = userManager;
            _settingsService = settingsService;
            _auditService = auditService;
        }

        private List<string> GetAvailableRoles()
        {
            return ApplicationRoles.All.ToList();
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var model = new AdminDashboardViewModel
            {
                UserCount = await _userManager.Users.CountAsync(),
                StudentCount = await _userManager.Users.CountAsync(user => user.RoleType == ApplicationRoles.Student),
                SupervisorCount = await _userManager.Users.CountAsync(user => user.RoleType == ApplicationRoles.Supervisor),
                ProjectGroupCount = await _context.ProjectGroups.CountAsync(),
                ProposalCount = await _context.ProjectProposals.CountAsync(),
                PendingProposalCount = await _context.ProjectProposals.CountAsync(projectProposal => projectProposal.Status == ProposalStatuses.Pending),
                UnderReviewProposalCount = await _context.ProjectProposals.CountAsync(projectProposal => projectProposal.Status == ProposalStatuses.UnderReview),
                MatchedProposalCount = await _context.ProjectProposals.CountAsync(projectProposal => projectProposal.Status == ProposalStatuses.Matched),
                ResearchAreaCount = await _context.ResearchAreas.CountAsync(),
                IsSubmissionWindowOpen = await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow),
                IsMatchingWindowOpen = await _settingsService.IsMatchingOpenAsync(DateTime.UtcNow),
                RecentAllocations = await _context.MatchRecords
                    .Include(match => match.ProjectProposal!)
                        .ThenInclude(projectProposal => projectProposal.ResearchArea)
                    .Include(match => match.Student)
                    .Include(match => match.Supervisor)
                    .OrderByDescending(match => match.MatchedAt)
                    .Take(5)
                    .Select(match => new AllocationSummaryViewModel
                    {
                        MatchId = match.Id,
                        ProjectProposalId = match.ProjectProposalId,
                        ProjectTitle = match.ProjectProposal != null ? match.ProjectProposal.Title : "Unavailable",
                        ResearchAreaName = match.ProjectProposal != null && match.ProjectProposal.ResearchArea != null
                            ? match.ProjectProposal.ResearchArea.Name
                            : "Unassigned",
                        StudentName = match.Student != null ? match.Student.FullName : "Unavailable",
                        StudentEmail = match.Student != null ? match.Student.Email ?? string.Empty : string.Empty,
                        SupervisorName = match.Supervisor != null ? match.Supervisor.FullName : "Unavailable",
                        SupervisorEmail = match.Supervisor != null ? match.Supervisor.Email ?? string.Empty : string.Empty,
                        MatchedAtUtc = match.MatchedAt
                    })
                    .ToListAsync(),
                RecentAuditLogs = await _context.AuditLogs
                    .OrderByDescending(auditLog => auditLog.OccurredAtUtc)
                    .Take(8)
                    .Select(auditLog => new AuditLogSummaryViewModel
                    {
                        Action = auditLog.Action,
                        ActorDisplayName = auditLog.ActorDisplayName,
                        Description = auditLog.Description,
                        OccurredAtUtc = auditLog.OccurredAtUtc,
                        IsSecurityEvent = auditLog.IsSecurityEvent
                    })
                    .ToListAsync(),
                RecentEmails = await _context.NotificationEmails
                    .OrderByDescending(notificationEmail => notificationEmail.CreatedAtUtc)
                    .Take(6)
                    .Select(notificationEmail => new NotificationEmailSummaryViewModel
                    {
                        ToEmail = notificationEmail.ToEmail,
                        Subject = notificationEmail.Subject,
                        DeliveryStatus = notificationEmail.DeliveryStatus,
                        NotificationType = notificationEmail.NotificationType,
                        CreatedAtUtc = notificationEmail.CreatedAtUtc
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        // =========================
        // Research Area Management
        // =========================

        [HttpGet]
        public async Task<IActionResult> ResearchAreas()
        {
            var areas = await _context.ResearchAreas
                .OrderBy(r => r.Name)
                .ToListAsync();

            return View(areas);
        }

        [HttpGet]
        public IActionResult CreateResearchArea()
        {
            return View(new ResearchArea());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateResearchArea(ResearchArea model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Name = NormalizeResearchAreaName(model.Name);

            var exists = await _context.ResearchAreas
                .AnyAsync(researchArea => researchArea.Name.ToLower() == model.Name.ToLower());

            if (exists)
            {
                ModelState.AddModelError("", "A research area with this name already exists.");
                return View(model);
            }

            _context.ResearchAreas.Add(model);
            await _context.SaveChangesAsync();

            var actor = await _userManager.GetUserAsync(User);
            await _auditService.LogAsync(
                "ResearchAreaCreated",
                nameof(ResearchArea),
                model.Id.ToString(),
                $"Research area '{model.Name}' was created.",
                actor?.Id,
                actor?.FullName);

            TempData["SuccessMessage"] = "Research area created successfully.";
            return RedirectToAction(nameof(ResearchAreas));
        }

        [HttpGet]
        public async Task<IActionResult> EditResearchArea(int id)
        {
            var area = await _context.ResearchAreas.FindAsync(id);

            if (area == null)
            {
                return NotFound();
            }

            return View(area);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditResearchArea(int id, ResearchArea model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var area = await _context.ResearchAreas.FindAsync(id);

            if (area == null)
            {
                return NotFound();
            }

            model.Name = NormalizeResearchAreaName(model.Name);

            var duplicateExists = await _context.ResearchAreas
                .AnyAsync(researchArea => researchArea.Id != id && researchArea.Name.ToLower() == model.Name.ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(nameof(model.Name), "A research area with this name already exists.");
                return View(model);
            }

            area.Name = model.Name;
            area.Description = model.Description;

            await _context.SaveChangesAsync();

            var actor = await _userManager.GetUserAsync(User);
            await _auditService.LogAsync(
                "ResearchAreaUpdated",
                nameof(ResearchArea),
                area.Id.ToString(),
                $"Research area '{area.Name}' was updated.",
                actor?.Id,
                actor?.FullName);

            TempData["SuccessMessage"] = "Research area updated successfully.";
            return RedirectToAction(nameof(ResearchAreas));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResearchArea(int id)
        {
            var area = await _context.ResearchAreas.FindAsync(id);

            if (area == null)
            {
                return NotFound();
            }

            var isUsed = await _context.ProjectProposals.AnyAsync(p => p.ResearchAreaId == id)
                        || await _context.SupervisorExpertise.AnyAsync(se => se.ResearchAreaId == id);

            if (isUsed)
            {
                TempData["ErrorMessage"] = "Cannot delete a research area that is already in use.";
                return RedirectToAction(nameof(ResearchAreas));
            }

            _context.ResearchAreas.Remove(area);
            await _context.SaveChangesAsync();

            var actor = await _userManager.GetUserAsync(User);
            await _auditService.LogAsync(
                "ResearchAreaDeleted",
                nameof(ResearchArea),
                id.ToString(),
                $"Research area '{area.Name}' was deleted.",
                actor?.Id,
                actor?.FullName);

            TempData["SuccessMessage"] = "Research area deleted successfully.";
            return RedirectToAction(nameof(ResearchAreas));
        }

        // =========================
        // User Management
        // =========================

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(users);
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            PrepareRoleDropdown();
            return View(new AdminCreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                PrepareRoleDropdown(model.SelectedRole);
                return View(model);
            }

            if (!IsValidRole(model.SelectedRole))
            {
                ModelState.AddModelError(nameof(model.SelectedRole), "Please select a valid system role.");
                PrepareRoleDropdown(model.SelectedRole);
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "A user with this email already exists.");
                PrepareRoleDropdown(model.SelectedRole);
                return View(model);
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Email,
                UserName = model.UserName,
                RoleType = model.SelectedRole,
                SupervisorCapacity = model.SelectedRole == ApplicationRoles.Supervisor ? model.SupervisorCapacity : null,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                PrepareRoleDropdown(model.SelectedRole);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.SelectedRole);

            var actor = await _userManager.GetUserAsync(User);
            await _auditService.LogAsync(
                "UserCreated",
                nameof(ApplicationUser),
                user.Id,
                $"User '{user.FullName}' was created with role '{model.SelectedRole}'.",
                actor?.Id,
                actor?.FullName,
                isSecurityEvent: true,
                metadata: new
                {
                    model.SelectedRole,
                    model.SupervisorCapacity
                });

            TempData["SuccessMessage"] = "User created successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var currentRole = roles.FirstOrDefault() ?? "";

            var model = new AdminUserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                SelectedRole = currentRole,
                SupervisorCapacity = user.SupervisorCapacity
            };

            PrepareRoleDropdown(model.SelectedRole);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(AdminUserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                PrepareRoleDropdown(model.SelectedRole);
                return View(model);
            }

            if (!IsValidRole(model.SelectedRole))
            {
                ModelState.AddModelError(nameof(model.SelectedRole), "Please select a valid system role.");
                PrepareRoleDropdown(model.SelectedRole);
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.RoleType = model.SelectedRole;
            user.SupervisorCapacity = model.SelectedRole == ApplicationRoles.Supervisor ? model.SupervisorCapacity : null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                PrepareRoleDropdown(model.SelectedRole);
                return View(model);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            await _userManager.AddToRoleAsync(user, model.SelectedRole);

            var actor = await _userManager.GetUserAsync(User);
            await _auditService.LogAsync(
                "UserUpdated",
                nameof(ApplicationUser),
                user.Id,
                $"User '{user.FullName}' was updated and assigned role '{model.SelectedRole}'.",
                actor?.Id,
                actor?.FullName,
                isSecurityEvent: true,
                metadata: new
                {
                    model.SelectedRole,
                    model.SupervisorCapacity
                });

            TempData["SuccessMessage"] = "User updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && currentUser.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot delete the account that is currently signed in.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var deletedName = user.FullName;

            if (await UserHasLinkedDataAsync(id))
            {
                TempData["ErrorMessage"] = "This user cannot be deleted because they are linked to proposals, project groups, expertise records, interests, or matches.";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to delete user.";
                return RedirectToAction(nameof(Users));
            }

            var actor = await _userManager.GetUserAsync(User);
            TempData["SuccessMessage"] = "User deleted successfully.";
            await _auditService.LogAsync(
                "UserDeleted",
                nameof(ApplicationUser),
                id,
                $"User '{deletedName}' was deleted.",
                actor?.Id,
                actor?.FullName,
                isSecurityEvent: true);
            return RedirectToAction(nameof(Users));
        }

        // =========================
        // Allocation Oversight
        // =========================

        [HttpGet]
        public async Task<IActionResult> Allocations()
        {
            var matches = await _context.MatchRecords
                .Include(m => m.ProjectProposal!)
                    .ThenInclude(p => p.ResearchArea)
                .Include(m => m.Student)
                .Include(m => m.Supervisor)
                .OrderByDescending(m => m.MatchedAt)
                .ToListAsync();

            return View(matches);
        }

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var settings = await _settingsService.GetSettingsAsync();
            var model = new SystemSettingsViewModel
            {
                ProposalSubmissionOpensAt = settings.ProposalSubmissionOpensAtUtc?.ToLocalTime(),
                ProposalSubmissionClosesAt = settings.ProposalSubmissionClosesAtUtc?.ToLocalTime(),
                MatchingOpensAt = settings.MatchingOpensAtUtc?.ToLocalTime(),
                MatchingClosesAt = settings.MatchingClosesAtUtc?.ToLocalTime(),
                DefaultSupervisorCapacity = settings.DefaultSupervisorCapacity,
                EmailNotificationsEnabled = settings.EmailNotificationsEnabled,
                RequireConfirmedAccountToSignIn = settings.RequireConfirmedAccountToSignIn,
                AllowOptionalTwoFactor = settings.AllowOptionalTwoFactor
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(SystemSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.ProposalSubmissionOpensAt.HasValue
                && model.ProposalSubmissionClosesAt.HasValue
                && model.ProposalSubmissionOpensAt > model.ProposalSubmissionClosesAt)
            {
                ModelState.AddModelError(nameof(model.ProposalSubmissionClosesAt), "Proposal closing time must be later than the opening time.");
            }

            if (model.MatchingOpensAt.HasValue
                && model.MatchingClosesAt.HasValue
                && model.MatchingOpensAt > model.MatchingClosesAt)
            {
                ModelState.AddModelError(nameof(model.MatchingClosesAt), "Matching closing time must be later than the opening time.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var actor = await _userManager.GetUserAsync(User);
            var updatedSettings = new SystemSettings
            {
                Id = 1,
                ProposalSubmissionOpensAtUtc = ToUtc(model.ProposalSubmissionOpensAt),
                ProposalSubmissionClosesAtUtc = ToUtc(model.ProposalSubmissionClosesAt),
                MatchingOpensAtUtc = ToUtc(model.MatchingOpensAt),
                MatchingClosesAtUtc = ToUtc(model.MatchingClosesAt),
                DefaultSupervisorCapacity = model.DefaultSupervisorCapacity,
                EmailNotificationsEnabled = model.EmailNotificationsEnabled,
                RequireConfirmedAccountToSignIn = model.RequireConfirmedAccountToSignIn,
                AllowSelfRegistration = false,
                AllowOptionalTwoFactor = model.AllowOptionalTwoFactor
            };

            await _settingsService.UpdateAsync(updatedSettings, actor?.Id);
            TempData["SuccessMessage"] = "System settings updated successfully.";
            return RedirectToAction(nameof(Settings));
        }

        // =========================
        // Manual Reassignment
        // =========================

        [HttpGet]
        public async Task<IActionResult> ReassignMatch(int id)
        {
            var match = await _context.MatchRecords
                .Include(m => m.ProjectProposal)
                .Include(m => m.Student)
                .Include(m => m.Supervisor)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (match == null)
            {
                return NotFound();
            }

            var supervisors = await _matchService.GetSupervisorCandidatesAsync();

            ViewBag.Supervisors = new SelectList(supervisors, "Id", "FullName", match.SupervisorId);

            var model = new ReassignMatchViewModel
            {
                MatchId = match.Id,
                ProjectProposalId = match.ProjectProposalId,
                CurrentSupervisorId = match.SupervisorId,
                NewSupervisorId = match.SupervisorId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReassignMatch(ReassignMatchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var supervisors = await _matchService.GetSupervisorCandidatesAsync();
                ViewBag.Supervisors = new SelectList(supervisors, "Id", "FullName", model.NewSupervisorId);
                return View(model);
            }

            var actor = await _userManager.GetUserAsync(User);
            var result = await _matchService.ReassignMatchAsync(model.MatchId, model.NewSupervisorId, actor?.Id, actor?.FullName);

            if (result.Status == MatchOperationStatus.NotFound)
            {
                return NotFound();
            }

            if (!result.Succeeded)
            {
                var supervisors = await _matchService.GetSupervisorCandidatesAsync();
                ViewBag.Supervisors = new SelectList(supervisors, "Id", "FullName", model.NewSupervisorId);
                TempData["ErrorMessage"] = result.Message;
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Allocations));
        }

        private void PrepareRoleDropdown(string? selectedRole = null)
        {
            ViewBag.Roles = new SelectList(GetAvailableRoles(), selectedRole);
        }

        private static bool IsValidRole(string role)
        {
            return ApplicationRoles.All.Contains(role);
        }

        private static string NormalizeResearchAreaName(string name)
        {
            return string.Join(' ', name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private async Task<bool> UserHasLinkedDataAsync(string userId)
        {
            return await _context.ProjectProposals.AnyAsync(projectProposal => projectProposal.StudentId == userId)
                || await _context.ProjectGroups.AnyAsync(projectGroup => projectGroup.LeadStudentId == userId)
                || await _context.ProjectGroupMembers.AnyAsync(projectGroupMember => projectGroupMember.StudentId == userId)
                || await _context.SupervisorInterests.AnyAsync(interest => interest.SupervisorId == userId)
                || await _context.MatchRecords.AnyAsync(matchRecord => matchRecord.StudentId == userId || matchRecord.SupervisorId == userId)
                || await _context.SupervisorExpertise.AnyAsync(expertise => expertise.SupervisorId == userId);
        }

        private static DateTime? ToUtc(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime();
        }
    }
}
