namespace BlindMatchPAS.ViewModels
{
    public class NotificationEmailSummaryViewModel
    {
        public string ToEmail { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string DeliveryStatus { get; set; } = string.Empty;

        public string NotificationType { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }
}
