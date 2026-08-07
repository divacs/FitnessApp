using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Memberships.Interfaces;
using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Application.Features.Payments.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Domain.Enums;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Payments;

public class PaymentServiceTests
{
    [Fact]
    public async Task CreatePaymentAsync_WhenPackage12_ShouldCreatePaymentAndBalance()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var adminId = Guid.NewGuid();
        var startDate = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = user.Id,
                Amount = 4500,
                PaymentDate = startDate,
                PaymentType = PurchaseType.Package12,
                StartDate = startDate,
                Note = "Paket 12"
            },
            adminId);

        response.UserId.Should().Be(user.Id);
        response.UserFullName.Should().Be(user.FullName);
        response.PaymentType.Should().Be(PurchaseType.Package12);
        response.NumberOfSessions.Should().Be(12);
        response.CreatedByAdminId.Should().Be(adminId);

        var payment = await dbContext.Payments.SingleAsync();
        payment.Amount.Should().Be(4500);
        payment.NumberOfSessions.Should().Be(12);

        var balance = await dbContext.UserTrainingBalances.SingleAsync();
        balance.UserId.Should().Be(user.Id);
        balance.PurchaseType.Should().Be(PurchaseType.Package12);
        balance.TotalSessions.Should().Be(12);
        balance.RemainingSessions.Should().Be(12);
        balance.StartDate.Should().Be(startDate);
        balance.EndDate.Should().Be(startDate.AddMonths(1));
        balance.CreatedByAdminId.Should().Be(adminId);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenSingleSessions_ShouldCreatePaymentAndSingleSessionsBalance()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = user.Id,
                Amount = 1500,
                PaymentDate = DateTime.UtcNow,
                PaymentType = PurchaseType.SingleSessions,
                NumberOfSessions = 3,
                Note = "Pojedinačni termini"
            },
            Guid.NewGuid());

        response.PaymentType.Should().Be(PurchaseType.SingleSessions);
        response.NumberOfSessions.Should().Be(3);

        var balance = await dbContext.UserTrainingBalances.SingleAsync();
        balance.PurchaseType.Should().Be(PurchaseType.SingleSessions);
        balance.TotalSessions.Should().Be(3);
        balance.RemainingSessions.Should().Be(3);
        balance.EndDate.Should().BeNull();
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenPackage6_ShouldCreatePaymentAndBalance()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var startDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = user.Id,
                Amount = 3000,
                PaymentDate = startDate,
                PaymentType = PurchaseType.Package6,
                StartDate = startDate
            },
            Guid.NewGuid());

        response.PaymentType.Should().Be(PurchaseType.Package6);
        response.NumberOfSessions.Should().Be(6);

        var balance = await dbContext.UserTrainingBalances.SingleAsync();
        balance.PurchaseType.Should().Be(PurchaseType.Package6);
        balance.TotalSessions.Should().Be(6);
        balance.RemainingSessions.Should().Be(6);
        balance.EndDate.Should().Be(startDate.AddMonths(1));
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenPackageIsRecordedAfterPastReservation_ShouldSettleOverdueReservation()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var training = new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = "Jutarnji trening",
            Description = string.Empty,
            StartTime = DateTime.UtcNow.AddDays(-2),
            EndTime = DateTime.UtcNow.AddDays(-2).AddHours(1),
            Capacity = 15,
            TrainerName = "Sara",
            Location = "Studio",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TrainingSessionId = training.Id,
            TrainingSession = training,
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow.AddDays(-2)
        };
        var paymentDate = DateTime.UtcNow;

        dbContext.Users.Add(user);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        await paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = user.Id,
                Amount = 3000,
                PaymentDate = paymentDate,
                PaymentType = PurchaseType.Package6,
                StartDate = paymentDate
            },
            Guid.NewGuid());

        var updatedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservation.Id);
        updatedReservation.Status.Should().Be(ReservationStatus.Attended);
        updatedReservation.AttendedAt.Should().Be(paymentDate);

        var balance = await dbContext.UserTrainingBalances.SingleAsync();
        balance.RemainingSessions.Should().Be(5);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenUserHasActivePackage_ShouldSettleOverdueReservationFromActivePackage()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var activePackageStartDate = DateTime.UtcNow.AddDays(-7);
        var activePackage = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package12,
            TotalSessions = 12,
            RemainingSessions = 2,
            StartDate = activePackageStartDate,
            EndDate = activePackageStartDate.AddMonths(1),
            IsActive = true,
            IsExpired = false,
            CreatedAt = activePackageStartDate
        };
        var training = new TrainingSession
        {
            Id = Guid.NewGuid(),
            Title = "Jutarnji trening",
            Description = string.Empty,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1),
            Capacity = 15,
            TrainerName = "Sara",
            Location = "Studio",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TrainingSessionId = training.Id,
            TrainingSession = training,
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow.AddDays(-1)
        };
        var futureStartDate = DateTime.UtcNow.AddDays(2);

        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(activePackage);
        dbContext.TrainingSessions.Add(training);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        await paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = user.Id,
                Amount = 3000,
                PaymentDate = DateTime.UtcNow,
                PaymentType = PurchaseType.Package12,
                StartDate = futureStartDate
            },
            Guid.NewGuid());

        var updatedReservation = await dbContext.Reservations.SingleAsync(x => x.Id == reservation.Id);
        updatedReservation.Status.Should().Be(ReservationStatus.Attended);

        var updatedActivePackage = await dbContext.UserTrainingBalances.SingleAsync(x => x.Id == activePackage.Id);
        updatedActivePackage.RemainingSessions.Should().Be(1);

        var futurePackage = await dbContext.UserTrainingBalances
            .Where(x => x.Id != activePackage.Id)
            .SingleAsync();
        futurePackage.RemainingSessions.Should().Be(12);
        futurePackage.StartDate.Should().Be(futureStartDate);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenMonthlyMembershipAlreadyExists_ShouldPersistQueuedStartDate()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var activePackageStartDate = DateTime.UtcNow.AddDays(-7);
        var activePackageEndDate = activePackageStartDate.AddMonths(1);
        dbContext.Users.Add(user);
        dbContext.UserTrainingBalances.Add(new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package12,
            TotalSessions = 12,
            RemainingSessions = 5,
            StartDate = activePackageStartDate,
            EndDate = activePackageEndDate,
            IsActive = true,
            IsExpired = false,
            CreatedAt = activePackageStartDate
        });
        await dbContext.SaveChangesAsync();

        var response = await paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = user.Id,
                Amount = 3000,
                PaymentDate = DateTime.UtcNow,
                PaymentType = PurchaseType.Package6,
                StartDate = DateTime.UtcNow
            },
            Guid.NewGuid());

        response.StartDate.Should().Be(activePackageEndDate);

        var createdPayment = await dbContext.Payments.OrderByDescending(payment => payment.CreatedAt).FirstAsync();
        createdPayment.StartDate.Should().Be(activePackageEndDate);
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenUserDoesNotExist_ShouldThrowNotFound()
    {
        var services = CreateServiceProvider();
        var paymentService = services.GetRequiredService<IPaymentService>();

        var act = () => paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = Guid.NewGuid(),
                Amount = 1000,
                PaymentDate = DateTime.UtcNow,
                PaymentType = PurchaseType.Package6,
                StartDate = DateTime.UtcNow
            },
            Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Korisnik nije pronađen.");
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenAmountIsNegative_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var paymentService = services.GetRequiredService<IPaymentService>();

        var act = () => paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = Guid.NewGuid(),
                Amount = -1,
                PaymentDate = DateTime.UtcNow,
                PaymentType = PurchaseType.SingleSessions,
                NumberOfSessions = 1
            },
            Guid.NewGuid());

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Iznos ne može biti negativan.");
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenPaymentDateIsMissing_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var paymentService = services.GetRequiredService<IPaymentService>();

        var act = () => paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = Guid.NewGuid(),
                Amount = 1000,
                PaymentDate = default,
                PaymentType = PurchaseType.SingleSessions,
                NumberOfSessions = 1
            },
            Guid.NewGuid());

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Datum uplate je obavezan.");
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenPackageStartDateIsMissing_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var paymentService = services.GetRequiredService<IPaymentService>();

        var act = () => paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = Guid.NewGuid(),
                Amount = 1000,
                PaymentDate = DateTime.UtcNow,
                PaymentType = PurchaseType.Package12
            },
            Guid.NewGuid());

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Datum početka je obavezan za paket.");
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenSingleSessionsNumberIsMissing_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var paymentService = services.GetRequiredService<IPaymentService>();

        var act = () => paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                UserId = Guid.NewGuid(),
                Amount = 1000,
                PaymentDate = DateTime.UtcNow,
                PaymentType = PurchaseType.SingleSessions
            },
            Guid.NewGuid());

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Broj termina mora biti veći od 0.");
    }

    [Fact]
    public async Task GetPaymentsAsync_ShouldReturnPaginatedPayments()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        dbContext.Users.Add(user);
        dbContext.Payments.AddRange(
            CreatePayment(user.Id, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreatePayment(user.Id, new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc)));
        await dbContext.SaveChangesAsync();

        var response = await paymentService.GetPaymentsAsync(page: 1, pageSize: 1);

        response.Page.Should().Be(1);
        response.PageSize.Should().Be(1);
        response.TotalCount.Should().Be(2);
        response.TotalPages.Should().Be(2);
        response.Items.Should().ContainSingle();
        response.Items.Single().PaymentDate.Should().Be(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetUserPaymentsAsync_ShouldReturnOnlyUserPayments()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var otherUser = CreateUser();
        dbContext.Users.AddRange(user, otherUser);
        dbContext.Payments.AddRange(
            CreatePayment(user.Id, DateTime.UtcNow),
            CreatePayment(otherUser.Id, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        var response = await paymentService.GetUserPaymentsAsync(user.Id, page: 1, pageSize: 20);

        response.TotalCount.Should().Be(1);
        response.Items.Should().ContainSingle();
        response.Items.Single().UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetPaymentsAsync_WhenFiltersAreApplied_ShouldReturnMatchingPayments()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser(firstName: "Ana", lastName: "Markovic");
        var otherUser = CreateUser(firstName: "Mila", lastName: "Petrovic");
        dbContext.Users.AddRange(user, otherUser);
        dbContext.Payments.AddRange(
            new Payment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Amount = 4500,
                PaymentDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                PaymentType = PurchaseType.Package12,
                NumberOfSessions = 12,
                CreatedAt = DateTime.UtcNow,
                Note = "Maj paket"
            },
            new Payment
            {
                Id = Guid.NewGuid(),
                UserId = otherUser.Id,
                Amount = 1500,
                PaymentDate = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                PaymentType = PurchaseType.SingleSessions,
                NumberOfSessions = 3,
                CreatedAt = DateTime.UtcNow,
                Note = "Pojedinacno"
            });
        await dbContext.SaveChangesAsync();

        var response = await paymentService.GetPaymentsAsync(
            page: 1,
            pageSize: 20,
            paymentType: PurchaseType.Package12,
            userId: user.Id,
            fromDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            toDate: new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            search: "Ana");

        response.TotalCount.Should().Be(1);
        response.Items.Should().ContainSingle();
        response.Items.Single().UserId.Should().Be(user.Id);
        response.Items.Single().PaymentType.Should().Be(PurchaseType.Package12);
    }

    [Fact]
    public async Task UpdatePaymentAsync_ShouldUpdatePaymentFields()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var payment = CreatePayment(user.Id, DateTime.UtcNow.AddDays(-1));
        dbContext.Users.Add(user);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var paymentDate = DateTime.UtcNow;
        var response = await paymentService.UpdatePaymentAsync(
            payment.Id,
            new UpdatePaymentRequest
            {
                Amount = 2500,
                PaymentDate = paymentDate,
                Note = "Ažurirana uplata"
            });

        response.Amount.Should().Be(2500);
        response.PaymentDate.Should().Be(paymentDate);
        response.Note.Should().Be("Ažurirana uplata");
    }

    [Fact]
    public async Task UpdatePaymentAsync_WhenAmountIsNegative_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var paymentService = services.GetRequiredService<IPaymentService>();

        var act = () => paymentService.UpdatePaymentAsync(
            Guid.NewGuid(),
            new UpdatePaymentRequest
            {
                Amount = -1,
                PaymentDate = DateTime.UtcNow
            });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Iznos ne može biti negativan.");
    }

    [Fact]
    public async Task UpdatePaymentAsync_WhenPaymentDateIsMissing_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var paymentService = services.GetRequiredService<IPaymentService>();

        var act = () => paymentService.UpdatePaymentAsync(
            Guid.NewGuid(),
            new UpdatePaymentRequest
            {
                Amount = 1000,
                PaymentDate = default
            });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Datum uplate je obavezan.");
    }

    [Fact]
    public async Task DeletePaymentAsync_ShouldRemovePayment()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var paymentService = services.GetRequiredService<IPaymentService>();
        var user = CreateUser();
        var payment = CreatePayment(user.Id, DateTime.UtcNow);
        var balance = new UserTrainingBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PurchaseType = PurchaseType.Package6,
            TotalSessions = 6,
            RemainingSessions = 4,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(29),
            IsActive = true,
            IsExpired = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.Users.Add(user);
        dbContext.Payments.Add(payment);
        dbContext.UserTrainingBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        await paymentService.DeletePaymentAsync(payment.Id);

        var paymentExists = await dbContext.Payments.AnyAsync(x => x.Id == payment.Id);
        paymentExists.Should().BeFalse();

        var balanceExists = await dbContext.UserTrainingBalances.AnyAsync(x => x.Id == balance.Id);
        balanceExists.Should().BeFalse();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IPaymentService, PaymentService>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser CreateUser(
        string firstName = "Test",
        string lastName = "User")
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = "+381600000000",
            UserStatus = UserStatus.Verified,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Payment CreatePayment(Guid userId, DateTime paymentDate)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 1000,
            PaymentDate = paymentDate,
            PaymentType = PurchaseType.Package6,
            NumberOfSessions = 6,
            CreatedAt = paymentDate
        };
    }
}
