using BlindMatchPAS.Constants;
using BlindMatchPAS.Models;

namespace BlindMatchPAS.Areas.Identity.Pages.Account
{
    internal static class AccountNavigation
    {
        public static string ResolvePostAuthDestination(ApplicationUser user, string? returnUrl, HttpContext httpContext)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl)
                && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
                && !IsDefaultLandingPage(returnUrl))
            {
                return returnUrl;
            }

            return user.RoleType switch
            {
                ApplicationRoles.Student => "/Student/Dashboard",
                ApplicationRoles.Supervisor => "/Supervisor/Dashboard",
                ApplicationRoles.ModuleLeader => "/Admin/Dashboard",
                ApplicationRoles.Admin => "/Admin/Dashboard",
                _ => "/"
            };
        }

        private static bool IsDefaultLandingPage(string returnUrl)
        {
            var normalized = returnUrl.Trim();
            return normalized == "/"
                || normalized.Equals("/Home", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("/Home/Index", StringComparison.OrdinalIgnoreCase);
        }
    }
}
