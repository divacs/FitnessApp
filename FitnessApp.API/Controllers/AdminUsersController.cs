using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Users.DTOs;
using FitnessApp.Application.Features.Users.Interfaces;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<UserListResponse>>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] UserStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool favoritesOnly = true,
        CancellationToken cancellationToken = default)
    {
        var users = await _userService.GetUsersAsync(
            page,
            pageSize,
            status,
            search,
            favoritesOnly,
            cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<UserListResponse>>.Success(users));
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> VerifyUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userService.VerifyUserAsync(id, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Korisnik je uspešno verifikovan."));
    }

    [HttpPost("{id:guid}/block")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> BlockUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userService.BlockUserAsync(id, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Korisnik je blokiran."));
    }

    [HttpPost("{id:guid}/unblock")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> UnblockUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userService.UnblockUserAsync(id, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Korisnik je odblokiran."));
    }

    [HttpPut("{id:guid}/favorite")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> SetFavorite(
        Guid id,
        [FromQuery] bool isFavorite,
        CancellationToken cancellationToken)
    {
        await _userService.SetFavoriteAsync(id, isFavorite, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Status omiljenog korisnika je ažuriran."));
    }
}
