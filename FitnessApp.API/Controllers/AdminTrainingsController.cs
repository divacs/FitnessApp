using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Trainings.DTOs;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/trainings")]
public class AdminTrainingsController : ControllerBase
{
    private readonly ITrainingService _trainingService;

    public AdminTrainingsController(ITrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TrainingSessionResponse>>> CreateTraining(
        CreateTrainingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var training = await _trainingService.CreateTrainingAsync(request, cancellationToken);

        return Ok(ApiResponse<TrainingSessionResponse>.Success(training, "Trening je uspešno kreiran."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TrainingSessionResponse>>> UpdateTraining(
        Guid id,
        UpdateTrainingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var training = await _trainingService.UpdateTrainingAsync(id, request, cancellationToken);

        return Ok(ApiResponse<TrainingSessionResponse>.Success(training, "Trening je uspešno ažuriran."));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<TrainingSessionResponse>>> CancelTraining(
        Guid id,
        CancelTrainingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var training = await _trainingService.CancelTrainingAsync(
            id,
            request.CancellationReason,
            cancellationToken);

        return Ok(ApiResponse<TrainingSessionResponse>.Success(training, "Trening je uspešno otkazan."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<EmptyResponse>>> DeleteTraining(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _trainingService.DeleteTrainingAsync(id, cancellationToken);

        return Ok(ApiResponse<EmptyResponse>.Success(EmptyResponse.Value, "Trening je uspešno obrisan."));
    }
}
