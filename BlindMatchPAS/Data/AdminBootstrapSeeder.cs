using BlindMatchPAS.Constants;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Data
{
    public static class AdminBootstrapSeeder
    {
        public static async Task SeedInitialCoordinatorAsync(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var hasCoordinator = await userManager.Users.AnyAsync(user =>
                user.RoleType == ApplicationRoles.Admin || user.RoleType == ApplicationRoles.ModuleLeader);

            if (hasCoordinator)
            {
                return;
            }

            var email = configuration["BootstrapAdmin:Email"] ?? "admin@blindmatch.local";
            var password = configuration["BootstrapAdmin:Password"] ?? "Admin12345!";
            var fullName = configuration["BootstrapAdmin:FullName"] ?? "System Administrator";
            var role = configuration["BootstrapAdmin:Role"] ?? ApplicationRoles.Admin;

            if (!ApplicationRoles.CoordinatorRoles.Contains(role))
            {
                role = ApplicationRoles.Admin;
            }

            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                if (!await userManager.IsInRoleAsync(existingUser, role))
                {
                    await userManager.AddToRoleAsync(existingUser, role);
                }

                existingUser.RoleType = role;
                existingUser.EmailConfirmed = true;
                await userManager.UpdateAsync(existingUser);
                return;
            }

            var user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                RoleType = role,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Unable to create bootstrap coordinator account. {errors}");
            }

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Unable to assign bootstrap coordinator role. {errors}");
            }
        }
    }
}
