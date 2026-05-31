using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Application.Features.Notifications.Interfaces;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

/// <summary>
/// Admin endpoint-i za pregled i slanje notifikacija.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/notifications")]
public class AdminNotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public AdminNotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Vraća paginiran pregled sistemskih notifikacija.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<NotificationResponse>>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] NotificationType? type = null,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationService.GetNotificationsAsync(
            page,
            pageSize,
            type,
            cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<NotificationResponse>>.Success(notifications));
    }

    /// <summary>
    /// Šalje globalnu notifikaciju svim korisnicima.
    /// </summary>
    [HttpPost("global")]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> SendGlobalNotification(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var notification = await _notificationService.SendGlobalNotificationAsync(
            request,
            adminId,
            cancellationToken);

        return Ok(ApiResponse<NotificationResponse>.Success(notification, "Notifikacija je poslata korisnicima."));
    }
}
