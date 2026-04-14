using BlindMatchPAS.Models;
using BlindMatchPAS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemSettingsService _settingsService;
        private readonly IAuditService _auditService;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ISystemSettingsService settingsService,
            IAuditService auditService,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _settingsService = settingsService;
            _auditService = auditService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ReturnUrl { get; set; } = string.Empty;

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

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var identifier = Input.Identifier.Trim();
            var user = await _userManager.FindByEmailAsync(identifier)
                ?? await _userManager.FindByNameAsync(identifier);

            if (user == null || string.IsNullOrWhiteSpace(user.UserName))
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            if (settings.RequireConfirmedAccountToSignIn && !user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "Please confirm your email before signing in.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                await _auditService.LogAsync(
                    "UserLoggedIn",
                    nameof(ApplicationUser),
                    user.Id,
                    $"User '{user.FullName}' logged in successfully.",
                    user.Id,
                    user.FullName,
                    isSecurityEvent: true);
                var destination = AccountNavigation.ResolvePostAuthDestination(user, returnUrl, HttpContext);
                return LocalRedirect(destination);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                await _auditService.LogAsync(
                    "UserLockedOut",
                    nameof(ApplicationUser),
                    user.Id,
                    $"User '{user.FullName}' was locked out after repeated failed login attempts.",
                    user.Id,
                    user.FullName,
                    isSecurityEvent: true);
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "Email or Username")]
            public string Identifier { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me")]
            public bool RememberMe { get; set; }
        }
    }
}
