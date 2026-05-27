using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Features.Terms.DTOs;
using FitnessApp.Application.Features.Terms.Interfaces;
using FitnessApp.Application.Features.Terms.Mappings;
using FitnessApp.Domain.Entities;
using FitnessApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessApp.Infrastructure.Services;

public class TermsService : ITermsService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TermsService> _logger;

    public TermsService(
        AppDbContext dbContext,
        ILogger<TermsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TermsResponse> GetTermsAsync(CancellationToken cancellationToken = default)
    {
        var termsPage = await _dbContext.TermsPages
            .AsNoTracking()
            .OrderByDescending(terms => terms.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return termsPage?.ToResponse() ?? new TermsResponse();
    }

    public async Task<TermsResponse> UpdateTermsAsync(
        UpdateTermsRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        if (adminId == Guid.Empty)
        {
            throw new BadRequestException("Admin je obavezan.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new BadRequestException("Sadržaj opštih uslova je obavezan.");
        }

        var termsPage = await _dbContext.TermsPages
            .OrderByDescending(terms => terms.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (termsPage is null)
        {
            termsPage = new TermsPage
            {
                Content = request.Content.Trim(),
                UpdatedAt = DateTime.UtcNow,
                UpdatedByAdminId = adminId
            };

            _dbContext.TermsPages.Add(termsPage);
        }
        else
        {
            termsPage.Content = request.Content.Trim();
            termsPage.UpdatedAt = DateTime.UtcNow;
            termsPage.UpdatedByAdminId = adminId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated terms page {TermsPageId} by admin {AdminId}.",
            termsPage.Id,
            adminId);

        return termsPage.ToResponse();
    }
}
