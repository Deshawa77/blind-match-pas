namespace BlindMatchPAS.ViewModels
{
    public class AuditLogSummaryViewModel
    {
        public string Action { get; set; } = string.Empty;

        public string ActorDisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime OccurredAtUtc { get; set; }

        public bool IsSecurityEvent { get; set; }
    }
}
