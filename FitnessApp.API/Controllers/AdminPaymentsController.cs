using FitnessApp.API.Extensions;
using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Payments.DTOs;
using FitnessApp.Application.Features.Payments.Interfaces;
using FitnessApp.Domain.Constants;
using FitnessApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

/// <summary>
/// Admin endpoint-i za pregled i evidenciju uplata.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin")]
public class AdminPaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public AdminPaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// Vraća paginiran pregled svih uplata uz opcione filtere.
    /// </summary>
    [HttpGet("payments")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<PaymentResponse>>>> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PurchaseType? paymentType = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentService.GetPaymentsAsync(
            page,
            pageSize,
            paymentType,
            userId,
            fromDate,
            toDate,
            search,
            cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<PaymentResponse>>.Success(payments));
    }

    /// <summary>
    /// Vraća paginiran pregled uplata za izabranog korisnika.
    /// </summary>
    /// <param name="userId">Identifikator korisnika.</param>
    /// <param name="page">Broj strane.</param>
    /// <param name="pageSize">Veličina strane.</param>
    /// <param name="paymentType">Opcioni filter po tipu kupovine.</param>
    /// <param name="fromDate">Opcioni početni datum filtera.</param>
    /// <param name="toDate">Opcioni krajnji datum filtera.</param>
    /// <param name="search">Opciona tekstualna pretraga.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpGet("users/{userId:guid}/payments")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<PaymentResponse>>>> GetUserPayments(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PurchaseType? paymentType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentService.GetUserPaymentsAsync(
            userId,
            page,
            pageSize,
            paymentType,
            fromDate,
            toDate,
            search,
            cancellationToken);

        return Ok(ApiResponse<PaginatedResponse<PaymentResponse>>.Success(payments));
    }

    /// <summary>
    /// Evidentira novu uplatu i vezanu kupovinu paketa ili termina.
    /// </summary>
    [HttpPost("payments")]
    public async Task<ActionResult<ApiResponse<PaymentResponse>>> CreatePayment(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();
        var payment = await _paymentService.CreatePaymentAsync(request, adminId, cancellationToken);

        return Ok(ApiResponse<PaymentResponse>.Success(payment, "Uplata je uspešno evidentirana."));
    }

    /// <summary>
    /// Ažurira postojeću uplatu.
    /// </summary>
    /// <param name="id">Identifikator uplate.</param>
    /// <param name="request">Podaci za izmenu uplate.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpPut("payments/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PaymentResponse>>> UpdatePayment(
        Guid id,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentService.UpdatePaymentAsync(id, request, cancellationToken);

        return Ok(ApiResponse<PaymentResponse>.Success(payment, "Uplata je uspešno ažurirana."));
    }

    /// <summary>
    /// Briše uplatu iz sistema.
    /// </summary>
    /// <param name="id">Identifikator uplate.</param>
    /// <param name="cancellationToken">Token za otkazivanje zahteva.</param>
    [HttpDelete("payments/{id:guid}")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> DeletePayment(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _paymentService.DeletePaymentAsync(id, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Uplata je uspešno obrisana."));
    }
}
