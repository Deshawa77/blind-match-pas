using System.ComponentModel.DataAnnotations;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlindMatchPAS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuditService _auditService;
        private readonly ILogger<LoginWith2faModel> _logger;

        public LoginWith2faModel(
            SignInManager<ApplicationUser> signInManager,
            IAuditService auditService,
            ILogger<LoginWith2faModel> logger)
        {
            _signInManager = signInManager;
            _auditService = auditService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public bool RememberMe { get; set; }

        [BindProperty]
        public string ReturnUrl { get; set; } = "/";

        public IActionResult OnGet(bool rememberMe, string? returnUrl = null)
        {
            RememberMe = rememberMe;
            ReturnUrl = returnUrl ?? "/";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
        {
            returnUrl ??= "/";
            ReturnUrl = returnUrl;
            RememberMe = rememberMe;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new InvalidOperationException("Unable to load the two-factor authentication user.");
            }

            var authenticatorCode = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, Input.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with 2fa.");
                await _auditService.LogAsync(
                    "TwoFactorLoginSucceeded",
                    nameof(ApplicationUser),
                    user.Id,
                    $"User '{user.FullName}' completed two-factor authentication.",
                    user.Id,
                    user.FullName,
                    isSecurityEvent: true);
                var destination = AccountNavigation.ResolvePostAuthDestination(user, returnUrl, HttpContext);
                return LocalRedirect(destination);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                await _auditService.LogAsync(
                    "TwoFactorLoginLockedOut",
                    nameof(ApplicationUser),
                    user.Id,
                    $"User '{user.FullName}' was locked out during two-factor authentication.",
                    user.Id,
                    user.FullName,
                    isSecurityEvent: true);
                return RedirectToPage("./Lockout");
            }

            await _auditService.LogAsync(
                "TwoFactorLoginFailed",
                nameof(ApplicationUser),
                user.Id,
                $"User '{user.FullName}' entered an invalid two-factor code.",
                user.Id,
                user.FullName,
                isSecurityEvent: true);

            ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            return Page();
        }

        public class InputModel
        {
            [Required]
            [StringLength(7, MinimumLength = 6)]
            [Display(Name = "Authenticator code")]
            public string TwoFactorCode { get; set; } = string.Empty;

            [Display(Name = "Remember this device")]
            public bool RememberMachine { get; set; }
        }
    }
}
