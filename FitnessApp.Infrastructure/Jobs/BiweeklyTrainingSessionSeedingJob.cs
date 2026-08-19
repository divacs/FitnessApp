using FitnessApp.Application.Features.Trainings.DTOs;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Jobs;

public class BiweeklyTrainingSessionSeedingJob
{
    public const string RecurringJobId = "biweekly-training-session-seeding";
    public const string CronExpression = "0 3 * * *";

    private const string DefaultTimeZoneId = "Europe/Belgrade";
    private const string WindowsFallbackTimeZoneId = "Central Europe Standard Time";
    private const string FallbackTrainingTitle = "Trening";
    private const string FixedTrainingTitle = "Aerobik";
    private const string FixedTrainingLocation = "Srnetička 4";
    private const int FixedTrainingCapacity = 15;
    private static readonly DateTime SeedStartDate = new(2026, 9, 1);
    private static readonly TrainingSlot[] DefaultSchedule =
    [
        new(DayOfWeek.Wednesday, 18, 0),
        new(DayOfWeek.Friday, 19, 0),
        new(DayOfWeek.Sunday, 10, 0)
    ];

    private readonly AppDbContext _dbContext;
    private readonly ITrainingService _trainingService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BiweeklyTrainingSessionSeedingJob> _logger;

    public BiweeklyTrainingSessionSeedingJob(
        AppDbContext dbContext,
        ITrainingService trainingService,
        TimeProvider timeProvider,
        ILogger<BiweeklyTrainingSessionSeedingJob> logger)
    {
        _dbContext = dbContext;
        _trainingService = trainingService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var timeZone = ResolveTimeZone();
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);

        var localWindowEndExclusive = localNow.AddDays(14);
        var template = await GetTemplateAsync(cancellationToken);
        var trainingRequests = await BuildMissingTrainingRequestsAsync(
            localNow,
            localWindowEndExclusive,
            timeZone,
            template,
            cancellationToken);

        if (trainingRequests.Count == 0)
        {
            _logger.LogInformation(
                "Training session seeding completed. No missing training sessions found in the next 14 days.");
            return;
        }

        foreach (var request in trainingRequests)
        {
            await _trainingService.CreateTrainingAsync(
                request,
                cancellationToken: cancellationToken,
                sendNotification: false);
        }

        _logger.LogInformation(
            "Training session seeding completed successfully. Created {CreatedCount} training sessions to keep the next 14 days covered.",
            trainingRequests.Count);
    }

    public static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsFallbackTimeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsFallbackTimeZoneId);
        }
    }

    private async Task<TrainingTemplate> GetTemplateAsync(CancellationToken cancellationToken)
    {
        var existingTrainingTemplate = await _dbContext.TrainingSessions
            .AsNoTracking()
            .OrderByDescending(training => training.StartTime)
            .ThenByDescending(training => training.CreatedAt)
            .Select(training => new TrainingTemplate(
                training.Title,
                training.Description,
                training.EndTime > training.StartTime
                    ? training.EndTime - training.StartTime
                    : TimeSpan.FromHours(1),
                FixedTrainingCapacity,
                training.TrainerName,
                training.Location))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingTrainingTemplate is not null)
        {
            return existingTrainingTemplate;
        }

        return new TrainingTemplate(
            FallbackTrainingTitle,
            string.Empty,
            TimeSpan.FromHours(1),
            FixedTrainingCapacity,
            "Sara",
            string.Empty);
    }

    private async Task<List<CreateTrainingSessionRequest>> BuildMissingTrainingRequestsAsync(
        DateTime localNow,
        DateTime localWindowEndExclusive,
        TimeZoneInfo timeZone,
        TrainingTemplate template,
        CancellationToken cancellationToken)
    {
        var candidateStartTimes = BuildCandidateStartTimes(localNow, localWindowEndExclusive, timeZone);
        if (candidateStartTimes.Count == 0)
        {
            return [];
        }

        var candidateUtcStartTimes = candidateStartTimes
            .Select(candidate => candidate.UtcStartTime)
            .ToArray();

        var existingUtcStartTimes = (await _dbContext.TrainingSessions
            .AsNoTracking()
            .Where(training => candidateUtcStartTimes.Contains(training.StartTime))
            .Select(training => training.StartTime)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var requests = new List<CreateTrainingSessionRequest>();

        foreach (var candidate in candidateStartTimes)
        {
            if (existingUtcStartTimes.Contains(candidate.UtcStartTime))
            {
                continue;
            }

            requests.Add(new CreateTrainingSessionRequest
            {
                Title = FixedTrainingTitle,
                Description = template.Description,
                StartTime = candidate.UtcStartTime,
                EndTime = candidate.UtcStartTime.Add(template.Duration),
                Capacity = template.Capacity,
                TrainerName = template.TrainerName,
                Location = FixedTrainingLocation
            });

            existingUtcStartTimes.Add(candidate.UtcStartTime);
        }

        return requests;
    }

    private static List<CandidateTrainingTime> BuildCandidateStartTimes(
        DateTime localNow,
        DateTime localWindowEndExclusive,
        TimeZoneInfo timeZone)
    {
        var candidates = new List<CandidateTrainingTime>();

        for (var date = localNow.Date; date < localWindowEndExclusive.Date.AddDays(1); date = date.AddDays(1))
        {
            foreach (var slot in DefaultSchedule)
            {
                if (date.DayOfWeek != slot.DayOfWeek)
                {
                    continue;
                }

                var localStartTime = date.AddHours(slot.Hour).AddMinutes(slot.Minute);

                if (localStartTime < SeedStartDate
                    || localStartTime <= localNow
                    || localStartTime >= localWindowEndExclusive)
                {
                    continue;
                }

                var utcStartTime = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localStartTime, DateTimeKind.Unspecified),
                    timeZone);

                candidates.Add(new CandidateTrainingTime(localStartTime, utcStartTime));
            }
        }

        return candidates;
    }

    private sealed record TrainingSlot(
        DayOfWeek DayOfWeek,
        int Hour,
        int Minute);

    private sealed record CandidateTrainingTime(
        DateTime LocalStartTime,
        DateTime UtcStartTime);

    private sealed record TrainingTemplate(
        string Title,
        string Description,
        TimeSpan Duration,
        int Capacity,
        string TrainerName,
        string Location);
}
