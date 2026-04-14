namespace BlindMatchPAS.Options
{
    public class EmailDeliveryOptions
    {
        public const string SectionName = "EmailDelivery";

        public string? FromAddress { get; set; }

        public string FromName { get; set; } = "BlindMatchPAS";

        public string? SmtpHost { get; set; }

        public int Port { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string BaseUrl { get; set; } = "https://localhost:5001";
    }
}
