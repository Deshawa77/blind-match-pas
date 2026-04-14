using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [StringLength(450)]
        public string? ActorUserId { get; set; }

        [StringLength(100)]
        public string ActorDisplayName { get; set; } = "System";

        [Required]
        [StringLength(120)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string EntityType { get; set; } = string.Empty;

        [StringLength(120)]
        public string? EntityId { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? MetadataJson { get; set; }

        public bool IsSecurityEvent { get; set; }

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    }
}
