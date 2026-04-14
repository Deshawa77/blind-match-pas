using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BlindMatchPAS.Tests.Functional
{
    public class SecurityJourneyTests : IClassFixture<BlindMatchWebAppFactory>
    {
        private readonly BlindMatchWebAppFactory _factory;

        public SecurityJourneyTests(BlindMatchWebAppFactory factory)
        {
            _factory = factory;
            _factory.EnsureInitializedAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task RegistrationPage_ShowsDisabledState_WhenSelfRegistrationIsDisabled()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.AllowSelfRegistration = false;
                settings.EmailNotificationsEnabled = false;
                settings.RequireConfirmedAccountToSignIn = false;
            });

            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            var homePage = await client.GetStringAsync("/");
            Assert.DoesNotContain(">Register<", homePage);

            var registerPage = await client.GetStringAsync("/Identity/Account/Register");
            Assert.Contains("Self-registration is disabled", registerPage);
        }

        [Fact]
        public async Task ForgotPasswordFlow_ResetsPasswordAndLogsNotification()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.EmailNotificationsEnabled = true;
                settings.RequireConfirmedAccountToSignIn = false;
            });

            var email = $"reset-user-{Guid.NewGuid():N}@blindmatch.local";
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Reset User", email, ApplicationRoles.Student, "Original12345!");

            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            var forgotToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Identity/Account/ForgotPassword");
            var forgotResponse = await client.PostAsync(
                "/Identity/Account/ForgotPassword",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = forgotToken,
                    ["Input.Email"] = email
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, forgotResponse.StatusCode);
            Assert.Contains("/Identity/Account/ForgotPasswordConfirmation", forgotResponse.Headers.Location?.OriginalString);

            var notification = await FunctionalTestSupport.GetLatestNotificationAsync(_factory, "PasswordReset");
            var resetUrl = FunctionalTestSupport.ExtractFirstHref(notification.HtmlBody);
            var resetToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, resetUrl);
            var resetResponse = await client.PostAsync(
                "/Identity/Account/ResetPassword",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = resetToken,
                    ["Input.Email"] = email,
                    ["Input.Code"] = GetQueryValue(resetUrl, "code"),
                    ["Input.Password"] = "Updated12345!",
                    ["Input.ConfirmPassword"] = "Updated12345!"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, resetResponse.StatusCode);
            Assert.Contains("/Identity/Account/ResetPasswordConfirmation", resetResponse.Headers.Location?.OriginalString);

            var loginResponse = await FunctionalTestSupport.LoginAsync(client, email, "Updated12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, loginResponse.StatusCode);
        }

        [Fact]
        public async Task RequireConfirmedAccount_BlocksLoginUntilEmailIsConfirmed()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.EmailNotificationsEnabled = true;
                settings.RequireConfirmedAccountToSignIn = true;
            });

            var email = $"confirm-user-{Guid.NewGuid():N}@blindmatch.local";
            var user = await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Confirm Required User", email, ApplicationRoles.Student, "Student12345!", emailConfirmed: false);
            var client = FunctionalTestSupport.CreateSecureClient(_factory);

            var blockedLoginResponse = await FunctionalTestSupport.LoginAsync(client, email, "Student12345!");
            Assert.Equal(System.Net.HttpStatusCode.OK, blockedLoginResponse.StatusCode);
            var blockedLoginPage = await blockedLoginResponse.Content.ReadAsStringAsync();
            Assert.Contains("Please confirm your email before signing in.", blockedLoginPage);

            var confirmUrl = await FunctionalTestSupport.GenerateEmailConfirmationUrlAsync(_factory, user);
            var confirmPage = await client.GetStringAsync(confirmUrl);
            Assert.Contains("Your email has been confirmed", confirmPage);

            var allowedLoginResponse = await FunctionalTestSupport.LoginAsync(client, email, "Student12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, allowedLoginResponse.StatusCode);
            Assert.Equal("/Student/Dashboard", allowedLoginResponse.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task OptionalTwoFactor_CanBeEnabledAndIsRequiredAtLogin()
        {
            await FunctionalTestSupport.UpdateSettingsAsync(_factory, settings =>
            {
                settings.AllowOptionalTwoFactor = true;
                settings.RequireConfirmedAccountToSignIn = false;
                settings.EmailNotificationsEnabled = false;
            });

            var email = $"twofactor-user-{Guid.NewGuid():N}@blindmatch.local";
            await FunctionalTestSupport.CreateUserWithRoleAsync(_factory, "Two Factor User", email, ApplicationRoles.Student, "Student12345!");
            var client = FunctionalTestSupport.CreateSecureClient(_factory);
            var loginResponse = await FunctionalTestSupport.LoginAsync(client, email, "Student12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, loginResponse.StatusCode);

            var enablePage = await client.GetStringAsync("/Identity/Account/Manage/EnableAuthenticator");
            var sharedKey = FunctionalTestSupport.ExtractSharedKey(enablePage);
            var enableToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Identity/Account/Manage/EnableAuthenticator");
            var enableResponse = await client.PostAsync(
                "/Identity/Account/Manage/EnableAuthenticator",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = enableToken,
                    ["Input.Code"] = FunctionalTestSupport.GenerateTotpCode(sharedKey)
                }));

            Assert.Equal(System.Net.HttpStatusCode.OK, enableResponse.StatusCode);
            var enabledPage = await enableResponse.Content.ReadAsStringAsync();
            Assert.Contains("Two-factor authentication is enabled", enabledPage);

            var logoutToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, "/Student/Dashboard");
            var logoutResponse = await client.PostAsync(
                "/Identity/Account/Logout",
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = logoutToken
                }));
            Assert.Equal(System.Net.HttpStatusCode.Redirect, logoutResponse.StatusCode);

            var passwordLoginResponse = await FunctionalTestSupport.LoginAsync(client, email, "Student12345!");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, passwordLoginResponse.StatusCode);
            Assert.Contains("/Identity/Account/LoginWith2fa", passwordLoginResponse.Headers.Location?.OriginalString);

            var twoFactorPath = passwordLoginResponse.Headers.Location!.OriginalString!;
            var twoFactorToken = await FunctionalTestSupport.GetAntiforgeryTokenAsync(client, twoFactorPath);
            var twoFactorResponse = await client.PostAsync(
                twoFactorPath,
                FunctionalTestSupport.BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = twoFactorToken,
                    ["Input.TwoFactorCode"] = FunctionalTestSupport.GenerateTotpCode(sharedKey),
                    ["Input.RememberMachine"] = "false",
                    ["RememberMe"] = "false",
                    ["ReturnUrl"] = "/"
                }));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, twoFactorResponse.StatusCode);
            Assert.Equal("/Student/Dashboard", twoFactorResponse.Headers.Location?.OriginalString);
        }

        private static string GetQueryValue(string url, string key)
        {
            var uri = new Uri(new Uri("https://localhost"), url);
            var pairs = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty, StringComparer.OrdinalIgnoreCase);

            return pairs[key];
        }
    }
}
