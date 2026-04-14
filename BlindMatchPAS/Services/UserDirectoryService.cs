using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Identity;

namespace BlindMatchPAS.Services
{
    public class UserDirectoryService : IUserDirectoryService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserDirectoryService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public Task<ApplicationUser?> FindByIdAsync(string userId)
        {
            return _userManager.FindByIdAsync(userId);
        }

        public Task<bool> IsInRoleAsync(ApplicationUser user, string role)
        {
            return _userManager.IsInRoleAsync(user, role);
        }

        public async Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string role)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            return users.OrderBy(user => user.FullName).ToList();
        }
    }
}
