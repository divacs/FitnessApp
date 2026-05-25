using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Application.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net;

namespace FitnessApp.Infrastructure.Emails;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> emailSettings,
        ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = CreateMessage(toEmail, subject, htmlBody, plainTextBody);

            using var smtpClient = new SmtpClient();

            await smtpClient.ConnectAsync(
                _emailSettings.SmtpHost,
                _emailSettings.SmtpPort,
                SecureSocketOptions.StartTls,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername)
                && !string.IsNullOrWhiteSpace(_emailSettings.SmtpPassword))
            {
                await smtpClient.AuthenticateAsync(
                    _emailSettings.SmtpUsername,
                    _emailSettings.SmtpPassword,
                    cancellationToken);
            }

            await smtpClient.SendAsync(message, cancellationToken);
            await smtpClient.DisconnectAsync(quit: true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {ToEmail} with subject {Subject}.", toEmail, subject);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Email send was cancelled for {ToEmail} with subject {Subject}.", toEmail, subject);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send email to {ToEmail} with subject {Subject}.", toEmail, subject);
        }
    }

    public Task SendRegistrationPendingApprovalEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken cancellationToken = default)
    {
        const string subject = "Nalog čeka verifikaciju";
        var plainTextBody = $"""
            Zdravo {firstName},

            Vaš nalog je uspešno kreiran. Potrebno je da admin verifikuje nalog. Kada verifikacija bude završena dobićete email.

            Sara - FitnessApp
            """;

        return SendAsync(
            toEmail,
            subject,
            BuildHtmlTemplate(
                "Nalog čeka verifikaciju",
                firstName,
                "Vaš nalog je uspešno kreiran. Potrebno je da admin verifikuje nalog. Kada verifikacija bude završena dobićete email."),
            plainTextBody,
            cancellationToken);
    }

    public Task SendUserVerifiedEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken cancellationToken = default)
    {
        const string subject = "Nalog je verifikovan";
        var plainTextBody = $"""
            Zdravo {firstName},

            Vaš nalog je uspešno verifikovan. Sada možete koristiti FitnessApp sistem.

            Sara - FitnessApp
            """;

        return SendAsync(
            toEmail,
            subject,
            BuildHtmlTemplate(
                "Nalog je verifikovan",
                firstName,
                "Vaš nalog je uspešno verifikovan. Sada možete koristiti FitnessApp sistem."),
            plainTextBody,
            cancellationToken);
    }

    public Task SendMembershipExpiringEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken cancellationToken = default)
    {
        const string subject = "Članarina uskoro ističe";
        var plainTextBody = $"""
            Zdravo {firstName},

            Vaša članarina ističe za 3 dana. Javite se Sari ako želite da produžite članarinu.

            Sara - FitnessApp
            """;

        return SendAsync(
            toEmail,
            subject,
            BuildHtmlTemplate(
                "Članarina uskoro ističe",
                firstName,
                "Vaša članarina ističe za 3 dana. Javite se Sari ako želite da produžite članarinu."),
            plainTextBody,
            cancellationToken);
    }

    private MimeMessage CreateMessage(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = plainTextBody
        }.ToMessageBody();

        return message;
    }

    private static string BuildHtmlTemplate(
        string title,
        string firstName,
        string message)
    {
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedFirstName = WebUtility.HtmlEncode(firstName);
        var encodedMessage = WebUtility.HtmlEncode(message);

        return $$"""
            <!doctype html>
            <html lang="sr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{encodedTitle}}</title>
            </head>
            <body style="margin:0;padding:0;background:#FFF8F3;font-family:Arial,sans-serif;color:#2F2933;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#FFF8F3;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#FFFFFF;border-radius:8px;padding:28px;border:1px solid #F3E6DD;">
                      <tr>
                        <td>
                          <p style="margin:0 0 8px;color:#9B6EF3;font-size:14px;font-weight:bold;">Sara - FitnessApp</p>
                          <h1 style="margin:0 0 18px;font-size:24px;line-height:1.25;color:#2F2933;">{{encodedTitle}}</h1>
                          <p style="margin:0 0 14px;font-size:16px;line-height:1.6;">Zdravo {{encodedFirstName}},</p>
                          <p style="margin:0;font-size:16px;line-height:1.6;">{{encodedMessage}}</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
