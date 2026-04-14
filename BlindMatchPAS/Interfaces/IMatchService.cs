using BlindMatchPAS.Models;
using BlindMatchPAS.Services;

namespace BlindMatchPAS.Interfaces
{
    public interface IMatchService
    {
        Task<MatchOperationResult> ExpressInterestAsync(int proposalId, string supervisorId);

        Task<MatchOperationResult> ConfirmMatchAsync(int interestId, string supervisorId);

        Task<MatchOperationResult> ReassignMatchAsync(int matchId, string newSupervisorId, string? actorUserId = null, string? actorDisplayName = null);

        Task<IReadOnlyList<ApplicationUser>> GetSupervisorCandidatesAsync();
    }
}
