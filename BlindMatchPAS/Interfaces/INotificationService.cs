using BlindMatchPAS.Models;

namespace BlindMatchPAS.Interfaces
{
    public interface INotificationService
    {
        Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string notificationType,
            string? relatedEntityType = null,
            string? relatedEntityId = null);

        Task SendRegistrationConfirmationAsync(ApplicationUser user, string confirmationUrl);
        Task SendProposalUnderReviewAsync(ProjectProposal proposal, string studentEmail, string researchAreaName);
        Task SendProposalUnderReviewAsync(ProjectProposal proposal, IReadOnlyCollection<ApplicationUser> students, string researchAreaName);
        Task SendIdentityRevealAsync(ProjectProposal proposal, ApplicationUser student, ApplicationUser supervisor, string researchAreaName);
        Task SendIdentityRevealAsync(ProjectProposal proposal, IReadOnlyCollection<ApplicationUser> students, ApplicationUser supervisor, string researchAreaName);
        Task SendPasswordResetAsync(ApplicationUser user, string resetUrl);
    }
}
