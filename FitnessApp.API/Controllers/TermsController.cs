using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Terms.DTOs;
using FitnessApp.Application.Features.Terms.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/terms")]
public class TermsController : ControllerBase
{
    private readonly ITermsService _termsService;

    public TermsController(ITermsService termsService)
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
}
