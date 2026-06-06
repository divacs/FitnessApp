using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FitnessApp.Infrastructure.Jobs;

public class TrainingReminderJob
{
    private const int ReminderHoursBeforeTraining = 24;
    private const int ReminderWindowMinutes = 30;

    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<TrainingReminderJob> _logger;

    public TrainingReminderJob(
        AppDbContext dbContext,
        IEmailService emailService,
        ILogger<TrainingReminderJob> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var utcNow = DateTime.UtcNow;
            var reminderWindowStart = utcNow.AddHours(ReminderHoursBeforeTraining);
            var reminderWindowEnd = reminderWindowStart.AddMinutes(ReminderWindowMinutes);

            var reservations = await _dbContext.Reservations
                .Include(reservation => reservation.User)
                .Include(reservation => reservation.TrainingSession)
                .Where(reservation =>
                    reservation.Status == ReservationStatus.Reserved
                    && reservation.ReminderSentAt == null
                    && reservation.TrainingSession.StartTime >= reminderWindowStart
                    && reservation.TrainingSession.StartTime < reminderWindowEnd)
                .OrderBy(reservation => reservation.TrainingSession.StartTime)
                .ToListAsync();

            foreach (var reservation in reservations)
            {
                try
                {
                    await SendReminderAsync(reservation);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Training reminder failed for reservation {ReservationId} and user {UserId}. Continuing with next reservation.",
                        reservation.Id,
                        reservation.UserId);
                }
            }

            _logger.LogInformation(
                "Training reminder job completed successfully. Processed reservations: {ReservationCount}.",
                reservations.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Training reminder job failed.");
            throw;
        }
    }

    private async Task SendReminderAsync(Reservation reservation)
    {
        if (string.IsNullOrWhiteSpace(reservation.User.Email))
        {
            _logger.LogWarning(
                "Training reminder skipped for reservation {ReservationId} because user {UserId} has no email.",
                reservation.Id,
                reservation.UserId);
            return;
        }

        var subject = "Podsetnik za trening";
        var plainTextBody = BuildPlainTextBody(reservation);
        var htmlBody = BuildHtmlBody(reservation);

        await _emailService.SendAsync(
            reservation.User.Email,
            subject,
            htmlBody,
            plainTextBody);

        reservation.ReminderSentAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Training reminder sent for reservation {ReservationId} to user {UserId}.",
            reservation.Id,
            reservation.UserId);
    }

    private static string BuildPlainTextBody(Reservation reservation)
    {
        return $"""
            Zdravo {reservation.User.FirstName},

            Podsećamo vas da imate rezervisan trening "{reservation.TrainingSession.Title}" koji počinje za 24h.

            Vreme: {reservation.TrainingSession.StartTime:dd.MM.yyyy. HH:mm}
            Lokacija: {reservation.TrainingSession.Location}
            Trener: {reservation.TrainingSession.TrainerName}

            Sara - FitnessApp
            """;
    }

    private static string BuildHtmlBody(Reservation reservation)
    {
        var firstName = WebUtility.HtmlEncode(reservation.User.FirstName);
        var trainingTitle = WebUtility.HtmlEncode(reservation.TrainingSession.Title);
        var location = WebUtility.HtmlEncode(reservation.TrainingSession.Location);
        var trainerName = WebUtility.HtmlEncode(reservation.TrainingSession.TrainerName);
        var startTime = WebUtility.HtmlEncode(reservation.TrainingSession.StartTime.ToString("dd.MM.yyyy. HH:mm"));

        return $$"""
            <!doctype html>
            <html lang="sr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Podsetnik za trening</title>
            </head>
            <body style="margin:0;padding:0;background:#FFF8F3;font-family:Arial,sans-serif;color:#2F2933;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#FFF8F3;padding:24px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#FFFFFF;border-radius:8px;padding:28px;border:1px solid #F3E6DD;">
                      <tr>
                        <td>
                          <p style="margin:0 0 8px;color:#9B6EF3;font-size:14px;font-weight:bold;">Sara - FitnessApp</p>
                          <h1 style="margin:0 0 18px;font-size:24px;line-height:1.25;color:#2F2933;">Podsetnik za trening</h1>
                          <p style="margin:0 0 14px;font-size:16px;line-height:1.6;">Zdravo {{firstName}},</p>
                          <p style="margin:0 0 16px;font-size:16px;line-height:1.6;">Podsećamo vas da imate rezervisan trening <strong>{{trainingTitle}}</strong> koji počinje za 24h.</p>
                          <p style="margin:0;font-size:16px;line-height:1.6;">Vreme: {{startTime}}<br>Lokacija: {{location}}<br>Trener: {{trainerName}}</p>
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
