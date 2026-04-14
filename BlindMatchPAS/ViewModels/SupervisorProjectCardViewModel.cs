namespace BlindMatchPAS.ViewModels
{
    public class SupervisorProjectCardViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Abstract { get; set; } = string.Empty;

        public string TechnicalStack { get; set; } = string.Empty;

        public string ResearchAreaName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool AlreadyInterested { get; set; }
    }
}
