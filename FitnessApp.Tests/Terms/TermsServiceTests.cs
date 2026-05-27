using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Terms.DTOs;
using FitnessApp.Application.Features.Terms.Interfaces;
using FitnessApp.Domain.Entities;
using FitnessApp.Infrastructure.Persistence;
using FitnessApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessApp.Tests.Terms;

public class TermsServiceTests
{
    [Fact]
    public async Task GetTermsAsync_WhenTermsDoNotExist_ShouldReturnEmptyResponse()
    {
        var services = CreateServiceProvider();
        var termsService = services.GetRequiredService<ITermsService>();

        var response = await termsService.GetTermsAsync();

        response.Id.Should().BeNull();
        response.Content.Should().BeEmpty();
        response.UpdatedAt.Should().BeNull();
        response.UpdatedByAdminId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTermsAsync_WhenTermsDoNotExist_ShouldCreateTermsPage()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var termsService = services.GetRequiredService<ITermsService>();
        var adminId = Guid.NewGuid();

        var response = await termsService.UpdateTermsAsync(
            new UpdateTermsRequest { Content = "  Opšti uslovi  " },
            adminId);

        response.Id.Should().NotBeNull();
        response.Content.Should().Be("Opšti uslovi");
        response.UpdatedByAdminId.Should().Be(adminId);
        response.UpdatedAt.Should().NotBeNull();

        var storedTerms = await dbContext.TermsPages.SingleAsync();
        storedTerms.Content.Should().Be("Opšti uslovi");
        storedTerms.UpdatedByAdminId.Should().Be(adminId);
    }

    [Fact]
    public async Task UpdateTermsAsync_WhenTermsExist_ShouldUpdateExistingTermsPage()
    {
        var services = CreateServiceProvider();
        var dbContext = services.GetRequiredService<AppDbContext>();
        var termsService = services.GetRequiredService<ITermsService>();
        var adminId = Guid.NewGuid();
        var termsPage = new TermsPage
        {
            Id = Guid.NewGuid(),
            Content = "Stari uslovi",
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.TermsPages.Add(termsPage);
        await dbContext.SaveChangesAsync();

        var response = await termsService.UpdateTermsAsync(
            new UpdateTermsRequest { Content = "Novi uslovi" },
            adminId);

        response.Id.Should().Be(termsPage.Id);
        response.Content.Should().Be("Novi uslovi");
        response.UpdatedByAdminId.Should().Be(adminId);

        var count = await dbContext.TermsPages.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task UpdateTermsAsync_WhenContentIsMissing_ShouldThrowBadRequest()
    {
        var services = CreateServiceProvider();
        var termsService = services.GetRequiredService<ITermsService>();

        var act = () => termsService.UpdateTermsAsync(
            new UpdateTermsRequest { Content = " " },
            Guid.NewGuid());

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Sadržaj opštih uslova je obavezan.");
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString());
        });
        services.AddScoped<ITermsService, TermsService>();

        return services.BuildServiceProvider();
    }
}
