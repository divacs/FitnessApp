using FitnessApp.Application.Features.Emails.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Jobs;

public class MembershipExpirationReminderJob
{
    private const int ReminderDaysBeforeExpiration = 3;

    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<MembershipExpirationReminderJob> _logger;

    public MembershipExpirationReminderJob(
        AppDbContext dbContext,
        IEmailService emailService,
        ILogger<MembershipExpirationReminderJob> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var expirationDate = DateTime.UtcNow.AddDays(ReminderDaysBeforeExpiration).Date;
            var expirationDayStart = expirationDate;
            var expirationDayEnd = expirationDayStart.AddDays(1);

            var balances = await _dbContext.UserTrainingBalances
                .Include(balance => balance.User)
                .Where(balance =>
                    balance.IsActive
                    && !balance.IsExpired
                    && balance.ExpirationReminderSentAt == null
                    && (balance.PurchaseType == PurchaseType.Package12
                        || balance.PurchaseType == PurchaseType.Package6)
                    && balance.EndDate >= expirationDayStart
                    && balance.EndDate < expirationDayEnd)
                .OrderBy(balance => balance.EndDate)
                .ToListAsync();

            foreach (var balance in balances)
            {
                try
                {
                    await SendReminderAsync(balance);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Membership expiration reminder failed for balance {BalanceId} and user {UserId}. Continuing with next balance.",
                        balance.Id,
                        balance.UserId);
                }
            }

            _logger.LogInformation(
                "Membership expiration reminder job completed successfully. Processed balances: {BalanceCount}.",
                balances.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Membership expiration reminder job failed.");
            throw;
        }
    }

    private async Task SendReminderAsync(UserTrainingBalance balance)
    {
        if (string.IsNullOrWhiteSpace(balance.User.Email))
        {
            _logger.LogWarning(
                "Membership expiration reminder skipped for balance {BalanceId} because user {UserId} has no email.",
                balance.Id,
                balance.UserId);
            return;
        }

        await _emailService.SendMembershipExpiringEmailAsync(
            balance.User.Email,
            balance.User.FirstName);

        balance.ExpirationReminderSentAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Membership expiration reminder sent for balance {BalanceId} to user {UserId}.",
            balance.Id,
            balance.UserId);
    }
}
