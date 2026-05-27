using FitnessApp.Application.Features.Emails.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FitnessApp.Infrastructure.Jobs;

public class NotificationEmailJob
{
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationEmailJob> _logger;

    public NotificationEmailJob(
        IEmailService emailService,
        ILogger<NotificationEmailJob> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string firstName,
        string title,
        string message)
    {
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedFirstName = WebUtility.HtmlEncode(firstName);
        var encodedMessage = WebUtility.HtmlEncode(message);

        var plainTextBody = $"""
            Zdravo {firstName},

            {message}

            Sara - FitnessApp
            """;

        var htmlBody = $$"""
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

        await _emailService.SendAsync(toEmail, title, htmlBody, plainTextBody);

        _logger.LogInformation("Notification email job completed for {ToEmail}.", toEmail);
    }
}
