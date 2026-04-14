using System.Diagnostics;
using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeDashboardViewModel
        {
            ResearchAreaCount = await _context.ResearchAreas.CountAsync(),
            ProposalCount = await _context.ProjectProposals.CountAsync(),
            MatchCount = await _context.MatchRecords.CountAsync(),
            SupervisorCount = await _context.Users.CountAsync(user => user.RoleType == ApplicationRoles.Supervisor),
            PendingProposalCount = await _context.ProjectProposals.CountAsync(projectProposal => projectProposal.Status == ProposalStatuses.Pending),
            UnderReviewProposalCount = await _context.ProjectProposals.CountAsync(projectProposal => projectProposal.Status == ProposalStatuses.UnderReview)
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
