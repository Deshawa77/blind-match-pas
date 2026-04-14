using System.ComponentModel.DataAnnotations;
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
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemSettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly IAuditService _auditService;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            ISystemSettingsService settingsService,
            INotificationService notificationService,
            IAuditService auditService)
        {
            _userManager = userManager;
            _settingsService = settingsService;
            _notificationService = notificationService;
            _auditService = auditService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code = encodedCode, email = user.Email },
                    protocol: Request.Scheme);

                var settings = await _settingsService.GetSettingsAsync();
                if (settings.EmailNotificationsEnabled && !string.IsNullOrWhiteSpace(callbackUrl))
                {
                    await _notificationService.SendPasswordResetAsync(user, callbackUrl);
                }

                await _auditService.LogAsync(
                    "PasswordResetRequested",
                    nameof(ApplicationUser),
                    user.Id,
                    $"A password reset was requested for '{user.Email}'.",
                    user.Id,
                    user.FullName,
                    isSecurityEvent: true);
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }
    }
}
