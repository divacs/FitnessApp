using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Terms.DTOs;
using FitnessApp.Application.Features.Terms.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/terms")]
public class AdminTermsController : ControllerBase
{
    private readonly ITermsService _termsService;

    public AdminTermsController(ITermsService termsService)
    {
        _termsService = termsService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<TermsResponse>>> GetTerms(
        CancellationToken cancellationToken)
    {
        var terms = await _termsService.GetTermsAsync(cancellationToken);

        return Ok(ApiResponse<TermsResponse>.Success(terms));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<TermsResponse>>> UpdateTerms(
        UpdateTermsRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var terms = await _termsService.UpdateTermsAsync(request, adminId, cancellationToken);

        return Ok(ApiResponse<TermsResponse>.Success(terms, "Opšti uslovi su uspešno ažurirani."));
    }
}
