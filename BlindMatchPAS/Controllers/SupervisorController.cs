using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Controllers
{
    [Authorize]
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupervisorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            var existing = await _context.SupervisorExpertise
                .Where(se => se.SupervisorId == user.Id)
                .ToListAsync();

            _context.SupervisorExpertise.RemoveRange(existing);

            if (model.SelectedResearchAreaIds.Any())
            {
                var newItems = model.SelectedResearchAreaIds.Select(id => new SupervisorExpertise
                {
                    SupervisorId = user.Id,
                    ResearchAreaId = id
                });

                await _context.SupervisorExpertise.AddRangeAsync(newItems);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Expertise preferences updated successfully.";
            return RedirectToAction(nameof(BrowseProjects));
        }

        [HttpGet]
        public async Task<IActionResult> BrowseProjects(int? researchAreaId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var expertiseIds = await _context.SupervisorExpertise
                .Where(se => se.SupervisorId == user.Id)
                .Select(se => se.ResearchAreaId)
                .ToListAsync();

            var query = _context.ProjectProposals
                .Include(p => p.ResearchArea)
                .Where(p => p.Status != "Withdrawn" && p.Status != "Matched" && !p.IsMatched);

            if (researchAreaId.HasValue)
            {
                query = query.Where(p => p.ResearchAreaId == researchAreaId.Value);
            }
            else if (expertiseIds.Any())
            {
                query = query.Where(p => expertiseIds.Contains(p.ResearchAreaId));
            }

            var proposals = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.ResearchAreas = new SelectList(
                await _context.ResearchAreas.OrderBy(r => r.Name).ToListAsync(),
                "Id",
                "Name",
                researchAreaId
            );

            return View(proposals);
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

            var proposal = await _context.ProjectProposals
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposal == null)
            {
                return NotFound();
            }

            if (proposal.Status == "Withdrawn" || proposal.Status == "Matched" || proposal.IsMatched)
            {
                TempData["ErrorMessage"] = "This project is no longer available.";
                return RedirectToAction(nameof(BrowseProjects));
            }

            var alreadyInterested = await _context.SupervisorInterests
                .AnyAsync(si => si.ProjectProposalId == id && si.SupervisorId == user.Id);

            if (alreadyInterested)
            {
                TempData["ErrorMessage"] = "You have already expressed interest in this project.";
                return RedirectToAction(nameof(BrowseProjects));
            }

            var interest = new SupervisorInterest
            {
                ProjectProposalId = proposal.Id,
                SupervisorId = user.Id,
                ExpressedAt = DateTime.UtcNow,
                IsConfirmed = false
            };

            _context.SupervisorInterests.Add(interest);

            if (proposal.Status == "Pending")
            {
                proposal.Status = "UnderReview";
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Interest expressed successfully.";
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

            var interests = await _context.SupervisorInterests
                .Include(si => si.ProjectProposal)
                    .ThenInclude(p => p.ResearchArea)
                .Where(si => si.SupervisorId == user.Id)
                .OrderByDescending(si => si.ExpressedAt)
                .ToListAsync();

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

            var interest = await _context.SupervisorInterests
                .Include(si => si.ProjectProposal)
                .FirstOrDefaultAsync(si => si.Id == id && si.SupervisorId == user.Id);

            if (interest == null)
            {
                return NotFound();
            }

            var proposal = interest.ProjectProposal;

            if (proposal == null)
            {
                return NotFound();
            }

            if (proposal.Status == "Matched" || proposal.IsMatched)
            {
                TempData["ErrorMessage"] = "This project has already been matched.";
                return RedirectToAction(nameof(MyInterests));
            }

            if (proposal.Status == "Withdrawn")
            {
                TempData["ErrorMessage"] = "This project has been withdrawn.";
                return RedirectToAction(nameof(MyInterests));
            }

            var existingMatch = await _context.MatchRecords
                .AnyAsync(m => m.ProjectProposalId == proposal.Id);

            if (existingMatch)
            {
                TempData["ErrorMessage"] = "A match record already exists for this project.";
                return RedirectToAction(nameof(MyInterests));
            }

            interest.IsConfirmed = true;

            proposal.Status = "Matched";
            proposal.IsMatched = true;
            proposal.IsIdentityRevealed = true;
            proposal.UpdatedAt = DateTime.UtcNow;

            var matchRecord = new MatchRecord
            {
                ProjectProposalId = proposal.Id,
                StudentId = proposal.StudentId,
                SupervisorId = user.Id,
                MatchedAt = DateTime.UtcNow,
                IdentityRevealed = true
            };

            _context.MatchRecords.Add(matchRecord);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Match confirmed successfully. Identity has been revealed.";
            return RedirectToAction(nameof(RevealedMatch), new { proposalId = proposal.Id });
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
                .Include(m => m.ProjectProposal)
                    .ThenInclude(p => p.ResearchArea)
                .Include(m => m.Student)
                .Include(m => m.Supervisor)
                .FirstOrDefaultAsync(m => m.ProjectProposalId == proposalId && m.SupervisorId == user.Id);

            if (match == null)
            {
                return NotFound();
            }

            return View(match);
        }
    }
}