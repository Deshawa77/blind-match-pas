using BlindMatchPAS.Constants;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Data;

namespace BlindMatchPAS.Tests.Functional
{
    public class BlindMatchWebAppFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private bool _initialized;

        public BlindMatchWebAppFactory()
        {
            Environment.SetEnvironmentVariable("BLINDMATCH_SKIP_STARTUP_INITIALIZATION", "true");
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            });
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipStartupInitialization"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));

                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }
                services.AddSingleton(_connection);
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            });
        }

        public async Task EnsureInitializedAsync()
        {
            if (_initialized)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            await RoleSeeder.SeedRoles(serviceProvider);

            if (!await context.SystemSettings.AnyAsync())
            {
                context.SystemSettings.Add(new SystemSettings
                {
                    Id = 1,
                    DefaultSupervisorCapacity = 4,
                    AllowSelfRegistration = false,
                    AllowOptionalTwoFactor = true,
                    EmailNotificationsEnabled = false,
                    RequireConfirmedAccountToSignIn = false
                });
            }

            if (!await context.ResearchAreas.AnyAsync())
            {
                context.ResearchAreas.Add(new ResearchArea
                {
                    Name = "Artificial Intelligence",
                    Description = "AI projects"
                });
            }

            await context.SaveChangesAsync();

            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminEmail = "admin@blindmatch.local";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    FullName = "System Admin",
                    Email = adminEmail,
                    UserName = adminEmail,
                    RoleType = ApplicationRoles.Admin,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(admin, "Admin12345!");
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException("Unable to seed admin test user.");
                }

                await userManager.AddToRoleAsync(admin, ApplicationRoles.Admin);
            }

            _initialized = true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Environment.SetEnvironmentVariable("BLINDMATCH_SKIP_STARTUP_INITIALIZATION", null);
                _connection.Dispose();
            }
        }
    }
}
