using System.Text;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace BlindMatchPAS.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class SecurityModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemSettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly IAuditService _auditService;

        public SecurityModel(
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

        [TempData]
        public string? StatusMessage { get; set; }

        public bool IsEmailConfirmed { get; private set; }

        public bool IsTwoFactorEnabled { get; private set; }

        public bool AllowOptionalTwoFactor { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSendConfirmationAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return RedirectToPage();
            }

            var settings = await _settingsService.GetSettingsAsync();
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var confirmationUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = encodedCode },
                protocol: Request.Scheme);

            if (settings.EmailNotificationsEnabled && !string.IsNullOrWhiteSpace(confirmationUrl))
            {
                await _notificationService.SendRegistrationConfirmationAsync(user, confirmationUrl);
            }

            StatusMessage = "Confirmation email sent.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDisableTwoFactorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage();
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _auditService.LogAsync(
                "TwoFactorDisabled",
                nameof(ApplicationUser),
                user.Id,
                $"User '{user.FullName}' disabled two-factor authentication.",
                user.Id,
                user.FullName,
                isSecurityEvent: true);

            StatusMessage = "Two-factor authentication disabled.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return;
            }

            var settings = await _settingsService.GetSettingsAsync();
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            AllowOptionalTwoFactor = settings.AllowOptionalTwoFactor;
        }
    }
}
