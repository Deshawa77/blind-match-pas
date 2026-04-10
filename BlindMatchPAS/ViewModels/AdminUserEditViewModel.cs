using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.ViewModels
{
    public class AdminUserEditViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role Type")]
        public string RoleType { get; set; } = string.Empty;

        [Required]
        [Display(Name = "System Role")]
        public string SelectedRole { get; set; } = string.Empty;
    }
}