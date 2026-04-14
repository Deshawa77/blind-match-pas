namespace BlindMatchPAS.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(
            string action,
            string entityType,
            string? entityId,
            string description,
            string? actorUserId = null,
            string? actorDisplayName = null,
            bool isSecurityEvent = false,
            object? metadata = null);
    }
}
