using BlindMatchPAS.Models;

namespace BlindMatchPAS.Interfaces
{
    public interface ISystemSettingsService
    {
        Task<SystemSettings> GetSettingsAsync();
        Task<SystemSettings> UpdateAsync(SystemSettings settings, string? actorUserId);
        Task<bool> IsProposalSubmissionOpenAsync(DateTime utcNow);
        Task<bool> IsMatchingOpenAsync(DateTime utcNow);
        Task<int> GetSupervisorCapacityAsync(ApplicationUser supervisor);
    }
}
