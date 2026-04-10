using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class SupervisorExpertise
    {
        public int Id { get; set; }

        [Required]
        public string SupervisorId { get; set; } = string.Empty;

        [ForeignKey("SupervisorId")]
        public ApplicationUser? Supervisor { get; set; }

        [Required]
        public int ResearchAreaId { get; set; }

        [ForeignKey("ResearchAreaId")]
        public ResearchArea? ResearchArea { get; set; }
    }
}