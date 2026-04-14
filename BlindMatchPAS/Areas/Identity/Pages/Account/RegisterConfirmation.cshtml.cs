using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlindMatchPAS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        public string Email { get; private set; } = string.Empty;

        public IActionResult OnGet(string? email)
        {
            Email = email ?? "your account";
            return Page();
        }
    }
}
