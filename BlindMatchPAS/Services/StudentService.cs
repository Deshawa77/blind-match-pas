using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using BlindMatchPAS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Services
{
    public class StudentService : IStudentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISystemSettingsService _settingsService;
        private readonly IAuditService _auditService;

        public StudentService(
            ApplicationDbContext context,
            ISystemSettingsService settingsService,
            IAuditService auditService)
        {
            _context = context;
            _settingsService = settingsService;
            _auditService = auditService;
        }

        public async Task<bool> CreateProposalAsync(string userId, ProjectProposalViewModel model)
        {
            if (!await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow))
            {
                return false;
            }

            var researchAreaExists = await _context.ResearchAreas
                .AnyAsync(researchArea => researchArea.Id == model.ResearchAreaId);

            if (!researchAreaExists)
            {
                return false;
            }

            int? projectGroupId = null;
            if (model.OwnershipType == ProposalOwnershipTypes.Group)
            {
                if (!model.ProjectGroupId.HasValue)
                {
                    return false;
                }

                var ownedGroup = await _context.ProjectGroups
                    .AnyAsync(projectGroup => projectGroup.Id == model.ProjectGroupId.Value && projectGroup.LeadStudentId == userId);

                if (!ownedGroup)
                {
                    return false;
                }

                projectGroupId = model.ProjectGroupId.Value;
            }
            else if (model.OwnershipType != ProposalOwnershipTypes.Individual)
            {
                return false;
            }

            var proposal = new ProjectProposal
            {
                Title = model.Title,
                Abstract = model.Abstract,
                TechnicalStack = model.TechnicalStack,
                ResearchAreaId = model.ResearchAreaId,
                StudentId = userId,
                ProjectGroupId = projectGroupId,
                Status = ProposalStatuses.Pending,
                IsMatched = false,
                IsIdentityRevealed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectProposals.Add(proposal);
            await _context.SaveChangesAsync();

            var student = await _context.Users.FirstOrDefaultAsync(user => user.Id == userId);
            await _auditService.LogAsync(
                "ProposalCreated",
                nameof(ProjectProposal),
                proposal.Id.ToString(),
                $"Project proposal '{proposal.Title}' was submitted.",
                userId,
                student?.FullName,
                metadata: new
                {
                    proposal.ResearchAreaId,
                    proposal.Status,
                    proposal.ProjectGroupId
                });

            return true;
        }

        public async Task<bool> WithdrawProposalAsync(int proposalId, string userId)
        {
            if (!await _settingsService.IsProposalSubmissionOpenAsync(DateTime.UtcNow))
            {
                return false;
            }

            var proposal = await _context.ProjectProposals
                .FirstOrDefaultAsync(p => p.Id == proposalId && p.StudentId == userId);

            if (proposal == null)
            {
                return false;
            }

            if (proposal.IsMatched || proposal.Status == ProposalStatuses.Matched || proposal.Status == ProposalStatuses.Withdrawn)
            {
                return false;
            }

            proposal.Status = ProposalStatuses.Withdrawn;
            proposal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var student = await _context.Users.FirstOrDefaultAsync(user => user.Id == userId);
            await _auditService.LogAsync(
                "ProposalWithdrawn",
                nameof(ProjectProposal),
                proposal.Id.ToString(),
                $"Project proposal '{proposal.Title}' was withdrawn.",
                userId,
                student?.FullName);

            return true;
        }
    }
}
