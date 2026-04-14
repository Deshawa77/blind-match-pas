using BlindMatchPAS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlindMatchPAS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly ISystemSettingsService _settingsService;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            ISystemSettingsService settingsService,
            ILogger<RegisterModel> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public string ReturnUrl { get; set; } = string.Empty;

        public bool IsSelfRegistrationEnabled => false;

        public bool RequireConfirmedAccountToSignIn { get; set; }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? "/";
            var settings = await _settingsService.GetSettingsAsync();
            RequireConfirmedAccountToSignIn = settings.RequireConfirmedAccountToSignIn;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= "/";
            ReturnUrl = returnUrl;
            var settings = await _settingsService.GetSettingsAsync();
            RequireConfirmedAccountToSignIn = settings.RequireConfirmedAccountToSignIn;
            _logger.LogInformation("Public registration was requested while self-registration is disabled.");
            ModelState.AddModelError(string.Empty, "Self-registration is disabled. Please contact the module leader or administrator for an account.");
            return Page();
        }
    }
}
