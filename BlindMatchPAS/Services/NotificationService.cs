using System.Net;
using System.Net.Mail;
using BlindMatchPAS.Data;
using BlindMatchPAS.Interfaces;
using BlindMatchPAS.Models;
using BlindMatchPAS.Options;
using Microsoft.Extensions.Options;

namespace BlindMatchPAS.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IOptions<EmailDeliveryOptions> _emailOptions;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            IOptions<EmailDeliveryOptions> emailOptions,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _emailOptions = emailOptions;
            _logger = logger;
        }

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string notificationType,
            string? relatedEntityType = null,
            string? relatedEntityId = null)
        {
            await DatabaseSchemaRecovery.ExecuteWithMigrationRecoveryAsync(
                _context,
                _logger,
                "NotificationEmails",
                async () =>
                {
                    var email = new NotificationEmail
                    {
                        ToEmail = toEmail,
                        Subject = subject,
                        HtmlBody = htmlBody,
                        NotificationType = notificationType,
                        RelatedEntityType = relatedEntityType,
                        RelatedEntityId = relatedEntityId,
                        DeliveryStatus = "Logged",
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    var options = _emailOptions.Value;
                    if (!string.IsNullOrWhiteSpace(options.FromAddress) && !string.IsNullOrWhiteSpace(options.SmtpHost))
                    {
                        try
                        {
                            using var smtpClient = new SmtpClient(options.SmtpHost, options.Port)
                            {
                                EnableSsl = options.EnableSsl
                            };

                            if (!string.IsNullOrWhiteSpace(options.Username))
                            {
                                smtpClient.Credentials = new NetworkCredential(options.Username, options.Password);
                            }

                            using var message = new MailMessage
                            {
                                From = new MailAddress(options.FromAddress, options.FromName),
                                Subject = subject,
                                Body = htmlBody,
                                IsBodyHtml = true
                            };

                            message.To.Add(toEmail);
                            await smtpClient.SendMailAsync(message);

                            email.DeliveryStatus = "Sent";
                            email.SentAtUtc = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            email.DeliveryStatus = "Failed";
                            email.FailureReason = ex.Message;
                            _logger.LogError(ex, "Failed to send email notification {NotificationType} to {ToEmail}.", notificationType, toEmail);
                        }
                    }

                    _context.NotificationEmails.Add(email);
                    await _context.SaveChangesAsync();
                });
        }

        public Task SendRegistrationConfirmationAsync(ApplicationUser user, string confirmationUrl)
        {
            var subject = "Confirm your BlindMatchPAS account";
            var body = $"""
                <p>Hello {WebUtility.HtmlEncode(user.FullName)},</p>
                <p>Welcome to BlindMatchPAS. Please confirm your email address to activate secure account recovery and optional two-factor authentication.</p>
                <p><a href="{WebUtility.HtmlEncode(confirmationUrl)}">Confirm your email</a></p>
                <p>If you did not create this account, you can ignore this message.</p>
                """;

            return SendEmailAsync(user.Email ?? string.Empty, subject, body, "RegistrationConfirmation", nameof(ApplicationUser), user.Id);
        }

        public Task SendProposalUnderReviewAsync(ProjectProposal proposal, string studentEmail, string researchAreaName)
        {
            var subject = "Your project proposal is now under review";
            var body = $"""
                <p>Your project proposal <strong>{WebUtility.HtmlEncode(proposal.Title)}</strong> has moved to the under-review stage.</p>
                <p>Research area: {WebUtility.HtmlEncode(researchAreaName)}</p>
                <p>A supervisor has expressed interest and the blind-review process is in progress.</p>
                """;

            return SendEmailAsync(studentEmail, subject, body, "ProposalUnderReview", nameof(ProjectProposal), proposal.Id.ToString());
        }

        public async Task SendProposalUnderReviewAsync(ProjectProposal proposal, IReadOnlyCollection<ApplicationUser> students, string researchAreaName)
        {
            var recipients = students
                .Where(student => !string.IsNullOrWhiteSpace(student.Email))
                .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            foreach (var student in recipients)
            {
                await SendProposalUnderReviewAsync(proposal, student.Email ?? string.Empty, researchAreaName);
            }
        }

        public async Task SendIdentityRevealAsync(ProjectProposal proposal, ApplicationUser student, ApplicationUser supervisor, string researchAreaName)
        {
            await SendIdentityRevealAsync(proposal, new[] { student }, supervisor, researchAreaName);
        }

        public async Task SendIdentityRevealAsync(ProjectProposal proposal, IReadOnlyCollection<ApplicationUser> students, ApplicationUser supervisor, string researchAreaName)
        {
            var recipients = students
                .Where(student => !string.IsNullOrWhiteSpace(student.Email))
                .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            foreach (var recipient in recipients)
            {
                var studentSubject = "Your project has been matched with a supervisor";
                var studentBody = $"""
                    <p>Your project <strong>{WebUtility.HtmlEncode(proposal.Title)}</strong> has been matched.</p>
                    <p>Supervisor: {WebUtility.HtmlEncode(supervisor.FullName)} ({WebUtility.HtmlEncode(supervisor.Email ?? string.Empty)})</p>
                    <p>Research area: {WebUtility.HtmlEncode(researchAreaName)}</p>
                    """;

                await SendEmailAsync(recipient.Email ?? string.Empty, studentSubject, studentBody, "IdentityReveal", nameof(ProjectProposal), proposal.Id.ToString());
            }

            var leadStudent = recipients.FirstOrDefault(studentItem => studentItem.Id == proposal.StudentId);
            var groupRoster = string.Join(", ", recipients.Select(studentItem => studentItem.FullName));
            var supervisorSubject = "Identity reveal complete for your new project match";
            var supervisorBody = proposal.ProjectGroupId.HasValue
                ? $"""
                    <p>You have confirmed a match for <strong>{WebUtility.HtmlEncode(proposal.Title)}</strong>.</p>
                    <p>Project group: {WebUtility.HtmlEncode(groupRoster)}</p>
                    <p>Lead contact: {WebUtility.HtmlEncode(leadStudent?.FullName ?? proposal.Student?.FullName ?? "Unavailable")} ({WebUtility.HtmlEncode(leadStudent?.Email ?? proposal.Student?.Email ?? string.Empty)})</p>
                    <p>Research area: {WebUtility.HtmlEncode(researchAreaName)}</p>
                    """
                : $"""
                    <p>You have confirmed a match for <strong>{WebUtility.HtmlEncode(proposal.Title)}</strong>.</p>
                    <p>Student: {WebUtility.HtmlEncode(leadStudent?.FullName ?? proposal.Student?.FullName ?? "Unavailable")} ({WebUtility.HtmlEncode(leadStudent?.Email ?? proposal.Student?.Email ?? string.Empty)})</p>
                    <p>Research area: {WebUtility.HtmlEncode(researchAreaName)}</p>
                    """;

            await SendEmailAsync(supervisor.Email ?? string.Empty, supervisorSubject, supervisorBody, "IdentityReveal", nameof(ProjectProposal), proposal.Id.ToString());
        }

        public Task SendPasswordResetAsync(ApplicationUser user, string resetUrl)
        {
            var subject = "Reset your BlindMatchPAS password";
            var body = $"""
                <p>Hello {WebUtility.HtmlEncode(user.FullName)},</p>
                <p>You requested a password reset for your BlindMatchPAS account.</p>
                <p><a href="{WebUtility.HtmlEncode(resetUrl)}">Reset your password</a></p>
                <p>If you did not request this, you can safely ignore this email.</p>
                """;

            return SendEmailAsync(user.Email ?? string.Empty, subject, body, "PasswordReset", nameof(ApplicationUser), user.Id);
        }
    }
}
