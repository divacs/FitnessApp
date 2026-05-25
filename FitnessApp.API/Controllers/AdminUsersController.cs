using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Users.DTOs;
using FitnessApp.Application.Features.Users.Interfaces;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.Admin)]
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
        CancellationToken cancellationToken = default)
    {
        var users = await _userService.GetUsersAsync(
            page,
            pageSize,
            status,
            search,
            cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<UserListResponse>>.Success(users));
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userService.VerifyUserAsync(id, cancellationToken);

        return Ok(ApiResponse<object>.Success(new { }, "Korisnik je uspešno verifikovan."));
    }

    [HttpPost("{id:guid}/block")]
    public async Task<ActionResult<ApiResponse<object>>> BlockUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userService.BlockUserAsync(id, cancellationToken);

        return Ok(ApiResponse<object>.Success(new { }, "Korisnik je blokiran."));
    }

    [HttpPost("{id:guid}/unblock")]
    public async Task<ActionResult<ApiResponse<object>>> UnblockUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userService.UnblockUserAsync(id, cancellationToken);

        return Ok(ApiResponse<object>.Success(new { }, "Korisnik je odblokiran."));
    }
}
