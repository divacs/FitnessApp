namespace FitnessApp.Application.Features.Emails.Interfaces;

public interface IEmailService
{
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default);

    Task SendRegistrationPendingApprovalEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken cancellationToken = default);

    Task SendUserVerifiedEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken cancellationToken = default);

    Task SendMembershipExpiringEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken cancellationToken = default);
}
