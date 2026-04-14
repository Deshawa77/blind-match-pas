using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlindMatchPAS.Tests.Unit
{
    public class SystemSettingsServiceTests
    {
        [Fact]
        public async Task GetSupervisorCapacityAsync_ReturnsDefaultCapacity_WhenSupervisorHasNoOverride()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            context.SystemSettings.Add(new BlindMatchPAS.Models.SystemSettings
            {
                Id = 1,
                DefaultSupervisorCapacity = 6,
                EmailNotificationsEnabled = false
            });
            await context.SaveChangesAsync();

            var service = new SystemSettingsService(context, Mock.Of<IAuditService>(), NullLogger<SystemSettingsService>.Instance);
            var supervisor = TestDbFactory.CreateUser("settings-supervisor", "Settings Supervisor", BlindMatchPAS.Constants.ApplicationRoles.Supervisor);

            var capacity = await service.GetSupervisorCapacityAsync(supervisor);

            Assert.Equal(6, capacity);
        }
    }
}
