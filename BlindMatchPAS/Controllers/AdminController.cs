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
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private List<string> GetAvailableRoles()
        {
            return new List<string> { "Student", "Supervisor", "ModuleLeader", "Admin" };
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

            var exists = await _context.ResearchAreas
                .AnyAsync(r => r.Name == model.Name);

            if (exists)
            {
                ModelState.AddModelError("", "A research area with this name already exists.");
                return View(model);
            }

            _context.ResearchAreas.Add(model);
            await _context.SaveChangesAsync();

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

            area.Name = model.Name;
            area.Description = model.Description;

            await _context.SaveChangesAsync();

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
            ViewBag.Roles = new SelectList(GetAvailableRoles());
            return View(new AdminCreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(GetAvailableRoles(), model.SelectedRole);
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "A user with this email already exists.");
                ViewBag.Roles = new SelectList(GetAvailableRoles(), model.SelectedRole);
                return View(model);
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Email,
                UserName = model.UserName,
                RoleType = model.RoleType,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                ViewBag.Roles = new SelectList(GetAvailableRoles(), model.SelectedRole);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.SelectedRole);

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
                RoleType = user.RoleType,
                SelectedRole = currentRole
            };

            ViewBag.Roles = new SelectList(GetAvailableRoles(), model.SelectedRole);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(AdminUserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(GetAvailableRoles(), model.SelectedRole);
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
            user.RoleType = model.RoleType;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                ViewBag.Roles = new SelectList(GetAvailableRoles(), model.SelectedRole);
                return View(model);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            await _userManager.AddToRoleAsync(user, model.SelectedRole);

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

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to delete user.";
                return RedirectToAction(nameof(Users));
            }

            TempData["SuccessMessage"] = "User deleted successfully.";
            return RedirectToAction(nameof(Users));
        }

        // =========================
        // Allocation Oversight
        // =========================

        [HttpGet]
        public async Task<IActionResult> Allocations()
        {
            var matches = await _context.MatchRecords
                .Include(m => m.ProjectProposal)
                    .ThenInclude(p => p.ResearchArea)
                .Include(m => m.Student)
                .Include(m => m.Supervisor)
                .OrderByDescending(m => m.MatchedAt)
                .ToListAsync();

            return View(matches);
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

            var supervisors = await _userManager.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

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
                var supervisors = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
                ViewBag.Supervisors = new SelectList(supervisors, "Id", "FullName", model.NewSupervisorId);
                return View(model);
            }

            var match = await _context.MatchRecords
                .Include(m => m.ProjectProposal)
                .FirstOrDefaultAsync(m => m.Id == model.MatchId);

            if (match == null)
            {
                return NotFound();
            }

            match.SupervisorId = model.NewSupervisorId;

            var proposal = match.ProjectProposal;
            if (proposal != null)
            {
                proposal.IsIdentityRevealed = true;
                proposal.IsMatched = true;
                proposal.Status = "Matched";
                proposal.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Match reassigned successfully.";
            return RedirectToAction(nameof(Allocations));
        }
    }
}