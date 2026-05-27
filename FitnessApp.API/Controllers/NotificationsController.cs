using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Application.Features.Notifications.Interfaces;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.VerifiedUsersOnly)]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<NotificationResponse>>>> GetMyNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] NotificationType? type = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var notifications = await _notificationService.GetMyNotificationsAsync(
            userId,
            page,
            pageSize,
            unreadOnly,
            type,
            cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<NotificationResponse>>.Success(notifications));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _notificationService.MarkAsReadAsync(userId, id, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Notifikacija je označena kao pročitana."));
    }
}
