using System.ComponentModel.DataAnnotations;
using System.Text;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlindMatchPAS.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class EnableAuthenticatorModel : PageModel
    {
        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemSettingsService _settingsService;
        private readonly IAuditService _auditService;

        public EnableAuthenticatorModel(
            UserManager<ApplicationUser> userManager,
            ISystemSettingsService settingsService,
            IAuditService auditService)
        {
            _userManager = userManager;
            _settingsService = settingsService;
            _auditService = auditService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string SharedKey { get; private set; } = string.Empty;

        public string AuthenticatorUri { get; private set; } = string.Empty;

        public IEnumerable<string> RecoveryCodes { get; private set; } = Array.Empty<string>();

        public async Task<IActionResult> OnGetAsync()
        {
            var settings = await _settingsService.GetSettingsAsync();
            if (!settings.AllowOptionalTwoFactor)
            {
                return RedirectToPage("./Security");
            }

            await LoadSharedKeyAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var settings = await _settingsService.GetSettingsAsync();
            if (!settings.AllowOptionalTwoFactor)
            {
                return RedirectToPage("./Security");
            }

            if (!ModelState.IsValid)
            {
                await LoadSharedKeyAsync();
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("./Security");
            }

            var verificationCode = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                verificationCode);

            if (!is2faTokenValid)
            {
                ModelState.AddModelError(string.Empty, "Verification code is invalid.");
                await LoadSharedKeyAsync();
                return Page();
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            RecoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8) ?? Array.Empty<string>();

            await _auditService.LogAsync(
                "TwoFactorEnabled",
                nameof(ApplicationUser),
                user.Id,
                $"User '{user.FullName}' enabled authenticator-based two-factor authentication.",
                user.Id,
                user.FullName,
                isSecurityEvent: true);

            await LoadSharedKeyAsync();
            return Page();
        }

        private async Task LoadSharedKeyAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return;
            }

            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            SharedKey = FormatKey(unformattedKey ?? string.Empty);
            AuthenticatorUri = string.Format(
                AuthenticatorUriFormat,
                Uri.EscapeDataString("BlindMatchPAS"),
                Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id),
                unformattedKey);
        }

        private static string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            var currentPosition = 0;

            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }

            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "Verification code")]
            public string Code { get; set; } = string.Empty;
        }
    }
}
