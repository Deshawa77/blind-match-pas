using Microsoft.AspNetCore.Identity;

namespace BlindMatchPAS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string RoleType { get; set; } = string.Empty;
    }
}