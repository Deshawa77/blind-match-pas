using System.Text;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace BlindMatchPAS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager, IAuditService auditService)
        {
            _userManager = userManager;
            _auditService = auditService;
        }

        public string StatusMessage { get; private set; } = string.Empty;

        public string? UserId { get; private set; }

        public async Task<IActionResult> OnGetAsync(string? userId, string? code, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                StatusMessage = "The email confirmation link is invalid.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                StatusMessage = "The account linked to this confirmation request could not be found.";
                return Page();
            }

            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, decodedCode);

            UserId = user.Id;
            StatusMessage = result.Succeeded
                ? "Your email has been confirmed. You can now use email confirmation, password recovery, and optional two-factor authentication."
                : "Email confirmation failed. The link may have expired or already been used.";

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    "EmailConfirmed",
                    nameof(ApplicationUser),
                    user.Id,
                    $"User '{user.FullName}' confirmed their email address.",
                    user.Id,
                    user.FullName,
                    isSecurityEvent: true);
            }

            return Page();
        }
    }
}
