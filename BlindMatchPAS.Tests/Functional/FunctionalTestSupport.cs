using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlindMatchPAS.Tests.Functional
{
    internal static class FunctionalTestSupport
    {
        public static HttpClient CreateSecureClient(BlindMatchWebAppFactory factory)
        {
            return factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        }

        public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
        {
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
            Assert.True(match.Success, $"Antiforgery token was not found on page {path}.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        public static FormUrlEncodedContent BuildFormContent(IEnumerable<KeyValuePair<string, string>> values)
        {
            return new FormUrlEncodedContent(values);
        }

        public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string identifier, string password)
        {
            var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");
            return await client.PostAsync(
                "/Identity/Account/Login",
                BuildFormContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["Input.Identifier"] = identifier,
                    ["Input.Password"] = password,
                    ["Input.RememberMe"] = "false"
                }));
        }

        public static async Task UpdateSettingsAsync(BlindMatchWebAppFactory factory, Action<SystemSettings> update)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await context.SystemSettings.SingleAsync();
            update(settings);
            await context.SaveChangesAsync();
        }

        public static async Task<ApplicationUser> CreateUserWithRoleAsync(
            BlindMatchWebAppFactory factory,
            string fullName,
            string email,
            string role,
            string password,
            bool emailConfirmed = true,
            int? supervisorCapacity = null)
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return existingUser;
            }

            var user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                RoleType = role,
                EmailConfirmed = emailConfirmed,
                SupervisorCapacity = role == ApplicationRoles.Supervisor ? supervisorCapacity : null
            };

            var createResult = await userManager.CreateAsync(user, password);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            return user;
        }

        public static async Task<string> GetUserIdByEmailAsync(BlindMatchWebAppFactory factory, string email)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        }

        public static async Task<NotificationEmail> GetLatestNotificationAsync(BlindMatchWebAppFactory factory, string notificationType)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.NotificationEmails
                .Where(notification => notification.NotificationType == notificationType)
                .OrderByDescending(notification => notification.CreatedAtUtc)
                .FirstAsync();
        }

        public static async Task<string> GenerateEmailConfirmationUrlAsync(BlindMatchWebAppFactory factory, ApplicationUser user, string returnUrl = "/")
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            return $"/Identity/Account/ConfirmEmail?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(encodedCode)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        public static string ExtractFirstHref(string html)
        {
            var match = Regex.Match(html, "href=\"([^\"]+)\"");
            Assert.True(match.Success, "Expected an anchor element with an href.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        public static string ExtractSharedKey(string html)
        {
            var match = Regex.Match(html, "Manual setup key</div>\\s*<code class=\"d-block\">([^<]+)</code>");
            Assert.True(match.Success, "Expected authenticator shared key on the enable authenticator page.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        public static string GenerateTotpCode(string formattedSharedKey)
        {
            var base32 = formattedSharedKey.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            var key = DecodeBase32(base32);
            var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
            var timestepBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(timestep));

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(timestepBytes);
            var offset = hash[^1] & 0x0F;
            var binaryCode =
                ((hash[offset] & 0x7F) << 24)
                | (hash[offset + 1] << 16)
                | (hash[offset + 2] << 8)
                | hash[offset + 3];

            return (binaryCode % 1_000_000).ToString("D6");
        }

        private static byte[] DecodeBase32(string input)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var output = new List<byte>();
            var bits = 0;
            var value = 0;

            foreach (var character in input.Where(character => character != '='))
            {
                var index = alphabet.IndexOf(character);
                if (index < 0)
                {
                    throw new InvalidOperationException($"Invalid base32 character '{character}'.");
                }

                value = (value << 5) | index;
                bits += 5;

                if (bits >= 8)
                {
                    output.Add((byte)((value >> (bits - 8)) & 0xFF));
                    bits -= 8;
                }
            }

            return output.ToArray();
        }
    }
}
