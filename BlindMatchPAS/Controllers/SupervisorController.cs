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
    [Authorize(Roles = ApplicationRoles.Supervisor)]
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMatchService _matchService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemSettingsService _settingsService;
        private readonly IAuditService _auditService;

        public SupervisorController(
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

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var expertiseAreas = await _context.SupervisorExpertise
                .Where(expertise => expertise.SupervisorId == user.Id)
                .Include(expertise => expertise.ResearchArea)
                .OrderBy(expertise => expertise.ResearchArea!.Name)
                .Select(expertise => expertise.ResearchArea != null ? expertise.ResearchArea.Name : "Unassigned")
                .ToListAsync();

            var availableProjectsQuery = _context.ProjectProposals
                .Where(projectProposal => projectProposal.Status != ProposalStatuses.Withdrawn
                    && projectProposal.Status != ProposalStatuses.Matched
                    && !projectProposal.IsMatched);

            var expertiseIds = await _context.SupervisorExpertise
                .Where(expertise => expertise.SupervisorId == user.Id)
                .Select(expertise => expertise.ResearchAreaId)
                .ToListAsync();

            if (expertiseIds.Any())
            {
                availableProjectsQuery = availableProjectsQuery
                    .Where(projectProposal => expertiseIds.Contains(projectProposal.ResearchAreaId));
            }

            var pendingInterests = await GetInterestSummariesAsync(user.Id);
            var settings = await _settingsService.GetSettingsAsync();
            var confirmedMatchCount = await _context.MatchRecords.CountAsync(matchRecord => matchRecord.SupervisorId == user.Id);
            var capacityLimit = await _settingsService.GetSupervisorCapacityAsync(user);

            var model = new SupervisorDashboardViewModel
            {
                SupervisorName = user.FullName,
                IsMatchingWindowOpen = await _settingsService.IsMatchingOpenAsync(DateTime.UtcNow),
                MatchingOpensAtUtc = settings.MatchingOpensAtUtc,
                MatchingClosesAtUtc = settings.MatchingClosesAtUtc,
                ExpertiseCount = expertiseAreas.Count,
                AvailableProjectCount = await availableProjectsQuery.CountAsync(),
                ConfirmedMatchCount = confirmedMatchCount,
                CapacityLimit = capacityLimit,
                RemainingCapacity = Math.Max(capacityLimit - confirmedMatchCount, 0),
                ExpertiseAreas = expertiseAreas,
                PendingInterests = pendingInterests.Where(interest => interest.CanConfirm).Take(5).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SetExpertise()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var selectedIds = await _context.SupervisorExpertise
                .Where(se => se.SupervisorId == user.Id)
                .Select(se => se.ResearchAreaId)
                .ToListAsync();

            ViewBag.ResearchAreas = await _context.ResearchAreas
                .OrderBy(r => r.Name)
                .ToListAsync();

            var model = new SupervisorExpertiseViewModel
            {
                SelectedResearchAreaIds = selectedIds
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetExpertise(SupervisorExpertiseViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var sanitizedResearchAreaIds = await _context.ResearchAreas
                .Where(researchArea => model.SelectedResearchAreaIds.Contains(researchArea.Id))
                .Select(researchArea => researchArea.Id)
                .ToListAsync();

            var existing = await _context.SupervisorExpertise
                .Where(se => se.SupervisorId == user.Id)
                .ToListAsync();

            _context.SupervisorExpertise.RemoveRange(existing);

            if (sanitizedResearchAreaIds.Any())
            {
                var newItems = sanitizedResearchAreaIds.Select(id => new SupervisorExpertise
                {
                    SupervisorId = user.Id,
                    ResearchAreaId = id
                });

                await _context.SupervisorExpertise.AddRangeAsync(newItems);
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "SupervisorExpertiseUpdated",
                nameof(SupervisorExpertise),
                user.Id,
                $"Supervisor '{user.FullName}' updated expertise preferences.",
                user.Id,
                user.FullName,
                metadata: new
                {
                    ResearchAreaIds = sanitizedResearchAreaIds
                });

            TempData["SuccessMessage"] = "Expertise preferences updated successfully.";
            return RedirectToAction(nameof(BrowseProjects));
        }

        [HttpGet]
        public async Task<IActionResult> BrowseProjects(int? researchAreaId, string? searchTerm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var settings = await _settingsService.GetSettingsAsync();
            var expertiseIds = await _context.SupervisorExpertise
                .Where(se => se.SupervisorId == user.Id)
                .Select(se => se.ResearchAreaId)
                .ToListAsync();

            var query = _context.ProjectProposals
                .Include(projectProposal => projectProposal.ResearchArea)
                .Where(projectProposal => projectProposal.Status != ProposalStatuses.Withdrawn
                    && projectProposal.Status != ProposalStatuses.Matched
                    && !projectProposal.IsMatched);

            if (researchAreaId.HasValue)
            {
                query = query.Where(projectProposal => projectProposal.ResearchAreaId == researchAreaId.Value);
            }
            else if (expertiseIds.Any())
            {
                query = query.Where(projectProposal => expertiseIds.Contains(projectProposal.ResearchAreaId));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearchTerm = searchTerm.Trim();

                query = query.Where(projectProposal =>
                    projectProposal.Title.Contains(normalizedSearchTerm)
                    || projectProposal.Abstract.Contains(normalizedSearchTerm)
                    || projectProposal.TechnicalStack.Contains(normalizedSearchTerm));
            }

            var interestedProjectIds = await _context.SupervisorInterests
                .Where(interest => interest.SupervisorId == user.Id)
                .Select(interest => interest.ProjectProposalId)
                .ToListAsync();

            var proposals = await query
                .OrderByDescending(projectProposal => projectProposal.CreatedAt)
                .Select(projectProposal => new SupervisorProjectCardViewModel
                {
                    Id = projectProposal.Id,
                    Title = projectProposal.Title,
                    Abstract = projectProposal.Abstract,
                    TechnicalStack = projectProposal.TechnicalStack,
                    ResearchAreaName = projectProposal.ResearchArea != null ? projectProposal.ResearchArea.Name : "Unassigned",
                    Status = projectProposal.Status,
                    CreatedAt = projectProposal.CreatedAt,
                    AlreadyInterested = interestedProjectIds.Contains(projectProposal.Id)
                })
                .ToListAsync();

            ViewBag.ResearchAreas = new SelectList(
                await _context.ResearchAreas.OrderBy(r => r.Name).ToListAsync(),
                "Id",
                "Name",
                researchAreaId
            );

            return View(new SupervisorBrowseProjectsViewModel
            {
                ResearchAreaId = researchAreaId,
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                HasConfiguredExpertise = expertiseIds.Any(),
                IsMatchingWindowOpen = await _settingsService.IsMatchingOpenAsync(DateTime.UtcNow),
                MatchingOpensAtUtc = settings.MatchingOpensAtUtc,
                MatchingClosesAtUtc = settings.MatchingClosesAtUtc,
                CapacityLimit = await _settingsService.GetSupervisorCapacityAsync(user),
                ConfirmedMatchCount = await _context.MatchRecords.CountAsync(matchRecord => matchRecord.SupervisorId == user.Id),
                RemainingCapacity = Math.Max(
                    await _settingsService.GetSupervisorCapacityAsync(user)
                        - await _context.MatchRecords.CountAsync(matchRecord => matchRecord.SupervisorId == user.Id),
                    0),
                Projects = proposals
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExpressInterest(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _matchService.ExpressInterestAsync(id, user.Id);

            if (result.Status == MatchOperationStatus.NotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(BrowseProjects));
        }

        [HttpGet]
        public async Task<IActionResult> MyInterests()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var interests = await GetInterestSummariesAsync(user.Id);

            return View(interests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmMatch(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _matchService.ConfirmMatchAsync(id, user.Id);

            if (result.Status == MatchOperationStatus.NotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            if (!result.Succeeded || result.ProposalId == null)
            {
                return RedirectToAction(nameof(MyInterests));
            }

            return RedirectToAction(nameof(RevealedMatch), new { proposalId = result.ProposalId.Value });
        }

        [HttpGet]
        public async Task<IActionResult> RevealedMatch(int proposalId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var match = await _context.MatchRecords
                .Include(m => m.ProjectProposal!)
                    .ThenInclude(p => p.ResearchArea)
                .Include(m => m.ProjectProposal!)
                    .ThenInclude(p => p.ProjectGroup)
                        .ThenInclude(projectGroup => projectGroup!.Members)
                            .ThenInclude(projectGroupMember => projectGroupMember.Student)
                .Include(m => m.Student)
                .Include(m => m.Supervisor)
                .FirstOrDefaultAsync(m => m.ProjectProposalId == proposalId && m.SupervisorId == user.Id);

            if (match == null)
            {
                return NotFound();
            }

            return View(match);
        }

        private Task<List<SupervisorInterestSummaryViewModel>> GetInterestSummariesAsync(string supervisorId)
        {
            return _context.SupervisorInterests
                .Include(interest => interest.ProjectProposal!)
                    .ThenInclude(projectProposal => projectProposal.ResearchArea)
                .Where(interest => interest.SupervisorId == supervisorId)
                .OrderByDescending(interest => interest.ExpressedAt)
                .Select(interest => new SupervisorInterestSummaryViewModel
                {
                    InterestId = interest.Id,
                    ProposalId = interest.ProjectProposalId,
                    ProjectTitle = interest.ProjectProposal != null ? interest.ProjectProposal.Title : "Unavailable",
                    ResearchAreaName = interest.ProjectProposal != null && interest.ProjectProposal.ResearchArea != null
                        ? interest.ProjectProposal.ResearchArea.Name
                        : "Unassigned",
                    Status = interest.ProjectProposal != null ? interest.ProjectProposal.Status : ProposalStatuses.Pending,
                    ExpressedAt = interest.ExpressedAt,
                    IsConfirmed = interest.IsConfirmed,
                    CanConfirm = interest.ProjectProposal != null
                        && !interest.ProjectProposal.IsMatched
                        && interest.ProjectProposal.Status != ProposalStatuses.Withdrawn
                })
                .ToListAsync();
        }
    }
}
