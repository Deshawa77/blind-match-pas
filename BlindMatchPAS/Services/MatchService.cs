using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Services
{
    public class MatchService : IMatchService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserDirectoryService _userDirectoryService;
        private readonly ISystemSettingsService _settingsService;
        private readonly IAuditService _auditService;
        private readonly INotificationService _notificationService;

        public MatchService(
            ApplicationDbContext context,
            IUserDirectoryService userDirectoryService,
            ISystemSettingsService settingsService,
            IAuditService auditService,
            INotificationService notificationService)
        {
            _context = context;
            _userDirectoryService = userDirectoryService;
            _settingsService = settingsService;
            _auditService = auditService;
            _notificationService = notificationService;
        }

        public async Task<MatchOperationResult> ExpressInterestAsync(int proposalId, string supervisorId)
        {
            var proposal = await _context.ProjectProposals
                .Include(projectProposal => projectProposal.Student)
                .Include(projectProposal => projectProposal.ResearchArea)
                .Include(projectProposal => projectProposal.ProjectGroup)
                    .ThenInclude(projectGroup => projectGroup!.Members)
                        .ThenInclude(projectGroupMember => projectGroupMember.Student)
                .FirstOrDefaultAsync(projectProposal => projectProposal.Id == proposalId);

            if (proposal == null)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.NotFound,
                    Message = "The selected project proposal could not be found."
                };
            }

            if (!await _settingsService.IsMatchingOpenAsync(DateTime.UtcNow))
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "The matching window is currently closed."
                };
            }

            var supervisor = await _context.Users.FirstOrDefaultAsync(user => user.Id == supervisorId);
            if (supervisor == null)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.NotFound,
                    Message = "The supervisor account could not be found."
                };
            }

            var currentMatchCount = await _context.MatchRecords.CountAsync(matchRecord => matchRecord.SupervisorId == supervisorId);
            var supervisorCapacity = await _settingsService.GetSupervisorCapacityAsync(supervisor);
            if (currentMatchCount >= supervisorCapacity)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = $"You have reached your supervision capacity of {supervisorCapacity} project(s)."
                };
            }

            if (proposal.Status == ProposalStatuses.Withdrawn || proposal.Status == ProposalStatuses.Matched || proposal.IsMatched)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "This project is no longer available."
                };
            }

            var alreadyInterested = await _context.SupervisorInterests
                .AnyAsync(interest => interest.ProjectProposalId == proposalId && interest.SupervisorId == supervisorId);

            if (alreadyInterested)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "You have already expressed interest in this project."
                };
            }

            var expertiseIds = await _context.SupervisorExpertise
                .Where(expertise => expertise.SupervisorId == supervisorId)
                .Select(expertise => expertise.ResearchAreaId)
                .ToListAsync();

            if (expertiseIds.Any() && !expertiseIds.Contains(proposal.ResearchAreaId))
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.Forbidden,
                    Message = "You can only express interest in projects that match your selected expertise areas."
                };
            }

            var interest = new SupervisorInterest
            {
                ProjectProposalId = proposal.Id,
                SupervisorId = supervisorId,
                ExpressedAt = DateTime.UtcNow,
                IsConfirmed = false
            };

            _context.SupervisorInterests.Add(interest);

            var wasPending = proposal.Status == ProposalStatuses.Pending;
            if (proposal.Status == ProposalStatuses.Pending)
            {
                proposal.Status = ProposalStatuses.UnderReview;
                proposal.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "InterestExpressed",
                nameof(ProjectProposal),
                proposal.Id.ToString(),
                $"Supervisor '{supervisor.FullName}' expressed interest in '{proposal.Title}'.",
                supervisorId,
                supervisor.FullName,
                metadata: new
                {
                    ProposalId = proposal.Id,
                    proposal.Title,
                    proposal.ResearchAreaId
                });

            if (wasPending && proposal.ResearchArea?.Name != null)
            {
                var settings = await _settingsService.GetSettingsAsync();
                if (settings.EmailNotificationsEnabled)
                {
                    var studentRecipients = GetStudentRecipients(proposal);
                    if (studentRecipients.Count > 0)
                    {
                        await _notificationService.SendProposalUnderReviewAsync(proposal, studentRecipients, proposal.ResearchArea.Name);
                    }
                }
            }

            return new MatchOperationResult
            {
                Status = MatchOperationStatus.Success,
                Message = "Interest expressed successfully.",
                ProposalId = proposal.Id
            };
        }

        public async Task<MatchOperationResult> ConfirmMatchAsync(int interestId, string supervisorId)
        {
            var interest = await _context.SupervisorInterests
                .Include(supervisorInterest => supervisorInterest.ProjectProposal)
                    .ThenInclude(projectProposal => projectProposal!.ResearchArea)
                .Include(supervisorInterest => supervisorInterest.ProjectProposal)
                    .ThenInclude(projectProposal => projectProposal!.Student)
                .Include(supervisorInterest => supervisorInterest.ProjectProposal)
                    .ThenInclude(projectProposal => projectProposal!.ProjectGroup)
                        .ThenInclude(projectGroup => projectGroup!.Members)
                            .ThenInclude(projectGroupMember => projectGroupMember.Student)
                .FirstOrDefaultAsync(supervisorInterest => supervisorInterest.Id == interestId && supervisorInterest.SupervisorId == supervisorId);

            if (interest == null)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.NotFound,
                    Message = "The selected expression of interest could not be found."
                };
            }

            var proposal = interest.ProjectProposal;
            if (proposal == null)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.NotFound,
                    Message = "The project proposal linked to this interest could not be found."
                };
            }

            if (!await _settingsService.IsMatchingOpenAsync(DateTime.UtcNow))
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "The matching window is currently closed."
                };
            }

            if (proposal.Status == ProposalStatuses.Withdrawn)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "This project has been withdrawn."
                };
            }

            if (proposal.Status == ProposalStatuses.Matched || proposal.IsMatched)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "This project has already been matched."
                };
            }

            var existingMatch = await _context.MatchRecords
                .AnyAsync(matchRecord => matchRecord.ProjectProposalId == proposal.Id);

            if (existingMatch)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "A match record already exists for this project."
                };
            }

            var supervisor = await _context.Users.FirstOrDefaultAsync(user => user.Id == supervisorId);
            if (supervisor == null)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.NotFound,
                    Message = "The supervisor account could not be found."
                };
            }

            var supervisorCapacity = await _settingsService.GetSupervisorCapacityAsync(supervisor);
            var currentMatchCount = await _context.MatchRecords.CountAsync(matchRecord => matchRecord.SupervisorId == supervisorId);
            if (currentMatchCount >= supervisorCapacity)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = $"You have reached your supervision capacity of {supervisorCapacity} project(s)."
                };
            }

            interest.IsConfirmed = true;

            proposal.Status = ProposalStatuses.Matched;
            proposal.IsMatched = true;
            proposal.IsIdentityRevealed = true;
            proposal.UpdatedAt = DateTime.UtcNow;

            var matchRecord = new MatchRecord
            {
                ProjectProposalId = proposal.Id,
                StudentId = proposal.StudentId,
                SupervisorId = supervisorId,
                MatchedAt = DateTime.UtcNow,
                IdentityRevealed = true
            };

            _context.MatchRecords.Add(matchRecord);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "MatchConfirmed",
                nameof(MatchRecord),
                matchRecord.Id.ToString(),
                $"Supervisor '{supervisor.FullName}' confirmed a match for '{proposal.Title}'.",
                supervisorId,
                supervisor.FullName,
                metadata: new
                {
                    ProposalId = proposal.Id,
                    StudentId = proposal.StudentId,
                    SupervisorId = supervisorId
                });

            if (proposal.ResearchArea?.Name != null)
            {
                var settings = await _settingsService.GetSettingsAsync();
                if (settings.EmailNotificationsEnabled)
                {
                    var studentRecipients = GetStudentRecipients(proposal);
                    if (studentRecipients.Count > 0)
                    {
                        await _notificationService.SendIdentityRevealAsync(proposal, studentRecipients, supervisor, proposal.ResearchArea.Name);
                    }
                }
            }

            return new MatchOperationResult
            {
                Status = MatchOperationStatus.Success,
                Message = "Match confirmed successfully. Identity has been revealed.",
                ProposalId = proposal.Id
            };
        }

        public async Task<MatchOperationResult> ReassignMatchAsync(int matchId, string newSupervisorId, string? actorUserId = null, string? actorDisplayName = null)
        {
            var match = await _context.MatchRecords
                .Include(matchRecord => matchRecord.ProjectProposal)
                    .ThenInclude(projectProposal => projectProposal!.ResearchArea)
                .Include(matchRecord => matchRecord.ProjectProposal)
                    .ThenInclude(projectProposal => projectProposal!.Student)
                .Include(matchRecord => matchRecord.ProjectProposal)
                    .ThenInclude(projectProposal => projectProposal!.ProjectGroup)
                        .ThenInclude(projectGroup => projectGroup!.Members)
                            .ThenInclude(projectGroupMember => projectGroupMember.Student)
                .FirstOrDefaultAsync(matchRecord => matchRecord.Id == matchId);

            if (match == null)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.NotFound,
                    Message = "The selected match could not be found."
                };
            }

            var supervisor = await _userDirectoryService.FindByIdAsync(newSupervisorId);
            if (supervisor == null || !await _userDirectoryService.IsInRoleAsync(supervisor, ApplicationRoles.Supervisor))
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "Reassignment is only allowed to a valid supervisor account."
                };
            }

            if (match.SupervisorId == newSupervisorId)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = "The selected supervisor is already assigned to this project."
                };
            }

            var supervisorCapacity = await _settingsService.GetSupervisorCapacityAsync(supervisor);
            var currentMatchCount = await _context.MatchRecords.CountAsync(matchRecord =>
                matchRecord.SupervisorId == newSupervisorId && matchRecord.Id != matchId);

            if (currentMatchCount >= supervisorCapacity)
            {
                return new MatchOperationResult
                {
                    Status = MatchOperationStatus.InvalidState,
                    Message = $"The selected supervisor has already reached the capacity limit of {supervisorCapacity} project(s)."
                };
            }

            if (match.ProjectProposal != null)
            {
                var expertiseIds = await _context.SupervisorExpertise
                    .Where(expertise => expertise.SupervisorId == newSupervisorId)
                    .Select(expertise => expertise.ResearchAreaId)
                    .ToListAsync();

                if (expertiseIds.Any() && !expertiseIds.Contains(match.ProjectProposal.ResearchAreaId))
                {
                    return new MatchOperationResult
                    {
                        Status = MatchOperationStatus.Forbidden,
                        Message = "The selected supervisor has not declared expertise in this project's research area."
                    };
                }
            }

            match.SupervisorId = newSupervisorId;

            if (match.ProjectProposal != null)
            {
                match.ProjectProposal.IsIdentityRevealed = true;
                match.ProjectProposal.IsMatched = true;
                match.ProjectProposal.Status = ProposalStatuses.Matched;
                match.ProjectProposal.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "MatchReassigned",
                nameof(MatchRecord),
                match.Id.ToString(),
                $"A coordinator reassigned project '{match.ProjectProposal?.Title}' to supervisor '{supervisor.FullName}'.",
                actorUserId,
                actorDisplayName,
                metadata: new
                {
                    MatchId = match.Id,
                    match.ProjectProposalId,
                    NewSupervisorId = newSupervisorId,
                    SupervisorName = supervisor.FullName
                });

            if (match.ProjectProposal?.ResearchArea?.Name != null)
            {
                var settings = await _settingsService.GetSettingsAsync();
                if (settings.EmailNotificationsEnabled)
                {
                    var studentRecipients = GetStudentRecipients(match.ProjectProposal);
                    if (studentRecipients.Count > 0)
                    {
                        await _notificationService.SendIdentityRevealAsync(match.ProjectProposal, studentRecipients, supervisor, match.ProjectProposal.ResearchArea.Name);
                    }
                }
            }

            return new MatchOperationResult
            {
                Status = MatchOperationStatus.Success,
                Message = "Match reassigned successfully.",
                ProposalId = match.ProjectProposalId
            };
        }

        public Task<IReadOnlyList<ApplicationUser>> GetSupervisorCandidatesAsync()
        {
            return _userDirectoryService.GetUsersInRoleAsync(ApplicationRoles.Supervisor);
        }

        private static IReadOnlyCollection<ApplicationUser> GetStudentRecipients(ProjectProposal proposal)
        {
            if (proposal.ProjectGroup?.Members != null && proposal.ProjectGroup.Members.Count > 0)
            {
                return proposal.ProjectGroup.Members
                    .Where(projectGroupMember => projectGroupMember.Student != null)
                    .Select(projectGroupMember => projectGroupMember.Student!)
                    .GroupBy(student => student.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToList();
            }

            return proposal.Student == null
                ? Array.Empty<ApplicationUser>()
                : new[] { proposal.Student };
        }
    }
}
