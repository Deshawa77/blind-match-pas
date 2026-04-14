using BlindMatchPAS.Data;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlindMatchPAS.Services
{
    public class SystemSettingsService : ISystemSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ILogger<SystemSettingsService> _logger;

        public SystemSettingsService(
            ApplicationDbContext context,
            IAuditService auditService,
            ILogger<SystemSettingsService> logger)
        {
            _context = context;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<SystemSettings> GetSettingsAsync()
        {
            return await DatabaseSchemaRecovery.ExecuteWithMigrationRecoveryAsync(
                _context,
                _logger,
                "SystemSettings",
                async () =>
                {
                    var settings = await _context.SystemSettings.SingleOrDefaultAsync();
                    if (settings != null)
                    {
                        if (settings.AllowSelfRegistration)
                        {
                            settings.AllowSelfRegistration = false;
                            settings.UpdatedAtUtc = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                        }

                        return settings;
                    }

                    settings = new SystemSettings
                    {
                        AllowSelfRegistration = false
                    };
                    _context.SystemSettings.Add(settings);
                    await _context.SaveChangesAsync();
                    return settings;
                });
        }

        public async Task<SystemSettings> UpdateAsync(SystemSettings settings, string? actorUserId)
        {
            settings.Id = 1;
            settings.AllowSelfRegistration = false;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedByUserId = actorUserId;

            settings = await DatabaseSchemaRecovery.ExecuteWithMigrationRecoveryAsync(
                _context,
                _logger,
                "SystemSettings",
                async () =>
                {
                    var existing = await _context.SystemSettings.SingleOrDefaultAsync();
                    if (existing == null)
                    {
                        _context.SystemSettings.Add(settings);
                    }
                    else
                    {
                        existing.ProposalSubmissionOpensAtUtc = settings.ProposalSubmissionOpensAtUtc;
                        existing.ProposalSubmissionClosesAtUtc = settings.ProposalSubmissionClosesAtUtc;
                        existing.MatchingOpensAtUtc = settings.MatchingOpensAtUtc;
                        existing.MatchingClosesAtUtc = settings.MatchingClosesAtUtc;
                        existing.DefaultSupervisorCapacity = settings.DefaultSupervisorCapacity;
                        existing.EmailNotificationsEnabled = settings.EmailNotificationsEnabled;
                        existing.RequireConfirmedAccountToSignIn = settings.RequireConfirmedAccountToSignIn;
                        existing.AllowSelfRegistration = false;
                        existing.AllowOptionalTwoFactor = settings.AllowOptionalTwoFactor;
                        existing.UpdatedAtUtc = settings.UpdatedAtUtc;
                        existing.UpdatedByUserId = settings.UpdatedByUserId;
                        settings = existing;
                    }

                    await _context.SaveChangesAsync();
                    return settings;
                });

            await _auditService.LogAsync(
                "SettingsUpdated",
                nameof(SystemSettings),
                settings.Id.ToString(),
                "System settings were updated.",
                actorUserId,
                actorUserId,
                isSecurityEvent: true,
                metadata: new
                {
                    settings.DefaultSupervisorCapacity,
                    settings.EmailNotificationsEnabled,
                    settings.RequireConfirmedAccountToSignIn,
                    settings.AllowSelfRegistration,
                    settings.AllowOptionalTwoFactor
                });

            return settings;
        }

        public async Task<bool> IsProposalSubmissionOpenAsync(DateTime utcNow)
        {
            var settings = await GetSettingsAsync();
            return IsWithinWindow(settings.ProposalSubmissionOpensAtUtc, settings.ProposalSubmissionClosesAtUtc, utcNow);
        }

        public async Task<bool> IsMatchingOpenAsync(DateTime utcNow)
        {
            var settings = await GetSettingsAsync();
            return IsWithinWindow(settings.MatchingOpensAtUtc, settings.MatchingClosesAtUtc, utcNow);
        }

        public async Task<int> GetSupervisorCapacityAsync(ApplicationUser supervisor)
        {
            ArgumentNullException.ThrowIfNull(supervisor);

            var settings = await GetSettingsAsync();
            return supervisor.SupervisorCapacity ?? settings.DefaultSupervisorCapacity;
        }

        private static bool IsWithinWindow(DateTime? opensAtUtc, DateTime? closesAtUtc, DateTime utcNow)
        {
            if (opensAtUtc.HasValue && utcNow < opensAtUtc.Value)
            {
                return false;
            }

            if (closesAtUtc.HasValue && utcNow > closesAtUtc.Value)
            {
                return false;
            }

            return true;
        }
    }
}
