using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Notifications.DTOs;
using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Notifications.Interfaces;

public interface INotificationService
{
    Task<NotificationResponse> CreateNotificationAsync(
        CreateNotificationRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> SendGlobalNotificationAsync(
        CreateNotificationRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<NotificationResponse>> GetMyNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly = false,
        NotificationType? type = null,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        Guid userId,
        Guid userNotificationId,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<NotificationResponse>> GetNotificationsAsync(
        int page,
        int pageSize,
        NotificationType? type = null,
        CancellationToken cancellationToken = default);

    Task SendTrainingCancelledNotificationsAsync(
        Guid trainingSessionId,
        string cancellationReason,
        CancellationToken cancellationToken = default);

    Task SendTrainingUpdatedNotificationsAsync(
        Guid trainingSessionId,
        CancellationToken cancellationToken = default);
}
