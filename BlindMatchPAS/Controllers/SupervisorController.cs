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

        // Set supervisor expertise
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

        // Browse anonymous proposals
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

        // Express interest in a project
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
    }
}