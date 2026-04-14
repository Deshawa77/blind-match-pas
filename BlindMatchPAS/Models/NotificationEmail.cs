using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.Models
{
    public class NotificationEmail
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string ToEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string HtmlBody { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        public string NotificationType { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string DeliveryStatus { get; set; } = "Logged";

        [StringLength(120)]
        public string? RelatedEntityType { get; set; }

        [StringLength(120)]
        public string? RelatedEntityId { get; set; }

        [StringLength(1000)]
        public string? FailureReason { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? SentAtUtc { get; set; }
    }
}
