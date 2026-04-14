using BlindMatchPAS.ViewModels;

namespace BlindMatchPAS.Interfaces
{
    public interface IStudentService
    {
        Task<bool> CreateProposalAsync(string userId, ProjectProposalViewModel model);
        Task<bool> WithdrawProposalAsync(int proposalId, string userId);
    }
}