using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Payments.DTOs;

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
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PaymentResponse>> GetUserPaymentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
