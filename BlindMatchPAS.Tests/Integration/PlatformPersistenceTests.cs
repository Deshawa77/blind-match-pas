using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using BlindMatchPAS.Options;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BlindMatchPAS.Tests.Integration
{
    public class PlatformPersistenceTests
    {
        [Fact]
        public async Task AuditService_LogAsync_PersistsAuditEntry()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var service = new AuditService(context, NullLogger<AuditService>.Instance);
            await service.LogAsync("DemoAction", nameof(ProjectProposal), "11", "Persisted audit record.");

            var log = await context.AuditLogs.SingleAsync();
            Assert.Equal("DemoAction", log.Action);
            Assert.Equal("Persisted audit record.", log.Description);
        }

        [Fact]
        public async Task NotificationService_SendEmailAsync_LogsNotificationWhenSmtpIsNotConfigured()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var options = Microsoft.Extensions.Options.Options.Create(new EmailDeliveryOptions());
            var service = new NotificationService(context, options, NullLogger<NotificationService>.Instance);

            await service.SendEmailAsync("notify@blindmatch.local", "Subject", "<p>Body</p>", "TestNotification");

            var notification = await context.NotificationEmails.SingleAsync();
            Assert.Equal("Logged", notification.DeliveryStatus);
            Assert.Equal("TestNotification", notification.NotificationType);
        }

        [Fact]
        public async Task SystemSettingsService_GetSettingsAsync_CreatesSingletonRow_WhenMissing()
        {
            using var connection = TestDbFactory.CreateConnection();
            await using var context = TestDbFactory.CreateContext(connection);

            var service = new SystemSettingsService(context, Mock.Of<IAuditService>(), NullLogger<SystemSettingsService>.Instance);

            var settings = await service.GetSettingsAsync();

            Assert.Equal(1, settings.Id);
            Assert.Single(await context.SystemSettings.ToListAsync());
        }
    }
}
