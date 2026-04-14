using System.Text.Json;
using BlindMatchPAS.Data;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using Microsoft.Extensions.Logging;

namespace BlindMatchPAS.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(
            string action,
            string entityType,
            string? entityId,
            string description,
            string? actorUserId = null,
            string? actorDisplayName = null,
            bool isSecurityEvent = false,
            object? metadata = null)
        {
            await DatabaseSchemaRecovery.ExecuteWithMigrationRecoveryAsync(
                _context,
                _logger,
                "AuditLogs",
                async () =>
                {
                    var entry = new AuditLog
                    {
                        ActorUserId = actorUserId,
                        ActorDisplayName = string.IsNullOrWhiteSpace(actorDisplayName) ? "System" : actorDisplayName,
                        Action = action,
                        EntityType = entityType,
                        EntityId = entityId,
                        Description = description,
                        MetadataJson = metadata == null ? null : JsonSerializer.Serialize(metadata),
                        IsSecurityEvent = isSecurityEvent,
                        OccurredAtUtc = DateTime.UtcNow
                    };

                    _context.AuditLogs.Add(entry);
                    await _context.SaveChangesAsync();
                });
        }
    }
}
