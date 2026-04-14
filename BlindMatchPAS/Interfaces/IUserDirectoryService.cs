using BlindMatchPAS.Models;

namespace BlindMatchPAS.Interfaces
{
    public interface IUserDirectoryService
    {
        Task<ApplicationUser?> FindByIdAsync(string userId);

        Task<bool> IsInRoleAsync(ApplicationUser user, string role);

        Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string role);
    }
}
