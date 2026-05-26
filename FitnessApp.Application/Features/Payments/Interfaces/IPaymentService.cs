using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Domain.Enums;

namespace FitnessApp.Application.Features.Payments.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> UpdatePaymentAsync(
        Guid paymentId,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task DeletePaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<PaymentResponse>> GetPaymentsAsync(
        int page,
        int pageSize,
        PurchaseType? paymentType = null,
        Guid? userId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<PaginatedResponse<PaymentResponse>> GetUserPaymentsAsync(
        Guid userId,
        int page,
        int pageSize,
        PurchaseType? paymentType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? search = null,
        CancellationToken cancellationToken = default);
}
