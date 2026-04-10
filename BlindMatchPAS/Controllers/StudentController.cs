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
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // View all proposals created by the logged-in student
        public async Task<IActionResult> MyProposals()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var proposals = await _context.ProjectProposals
                .Include(p => p.ResearchArea)
                .Where(p => p.StudentId == user.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(proposals);
        }

        // Show create form
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadResearchAreasAsync();
            return View(new ProjectProposalViewModel());
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

            if (!ModelState.IsValid)
            {
                await LoadResearchAreasAsync(model.ResearchAreaId);
                return View(model);
            }

            var proposal = new ProjectProposal
            {
                Title = model.Title,
                Abstract = model.Abstract,
                TechnicalStack = model.TechnicalStack,
                ResearchAreaId = model.ResearchAreaId,
                StudentId = user.Id,
                Status = "Pending",
                IsMatched = false,
                IsIdentityRevealed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectProposals.Add(proposal);
            await _context.SaveChangesAsync();

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
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

            if (proposal == null)
            {
                return NotFound();
            }

            if (proposal.IsMatched || proposal.Status == "Matched" || proposal.Status == "Withdrawn")
            {
                TempData["ErrorMessage"] = "You cannot edit a matched or withdrawn proposal.";
                return RedirectToAction(nameof(MyProposals));
            }

            var model = new ProjectProposalViewModel
            {
                Id = proposal.Id,
                Title = proposal.Title,
                Abstract = proposal.Abstract,
                TechnicalStack = proposal.TechnicalStack,
                ResearchAreaId = proposal.ResearchAreaId
            };

            await LoadResearchAreasAsync(model.ResearchAreaId);
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

            var proposal = await _context.ProjectProposals
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

            if (proposal == null)
            {
                return NotFound();
            }

            if (proposal.IsMatched || proposal.Status == "Matched" || proposal.Status == "Withdrawn")
            {
                TempData["ErrorMessage"] = "You cannot edit a matched or withdrawn proposal.";
                return RedirectToAction(nameof(MyProposals));
            }

            if (!ModelState.IsValid)
            {
                await LoadResearchAreasAsync(model.ResearchAreaId);
                return View(model);
            }

            proposal.Title = model.Title;
            proposal.Abstract = model.Abstract;
            proposal.TechnicalStack = model.TechnicalStack;
            proposal.ResearchAreaId = model.ResearchAreaId;
            proposal.UpdatedAt = DateTime.UtcNow;

            _context.ProjectProposals.Update(proposal);
            await _context.SaveChangesAsync();

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

            if (proposal.IsMatched || proposal.Status == "Matched")
            {
                TempData["ErrorMessage"] = "You cannot withdraw a matched proposal.";
                return RedirectToAction(nameof(MyProposals));
            }

            proposal.Status = "Withdrawn";
            proposal.UpdatedAt = DateTime.UtcNow;

            _context.ProjectProposals.Update(proposal);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Proposal withdrawn successfully.";
            return RedirectToAction(nameof(MyProposals));
        }

        private async Task LoadResearchAreasAsync(int? selectedId = null)
        {
            var researchAreas = await _context.ResearchAreas
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.ResearchAreas = new SelectList(researchAreas, "Id", "Name", selectedId);
        }
    }
}