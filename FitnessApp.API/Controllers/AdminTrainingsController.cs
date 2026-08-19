using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Trainings.DTOs;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Domain.Constants;
using FitnessApp.Infrastructure.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.AdminOnly)]
[Route("api/admin/trainings")]
public class AdminTrainingsController : ControllerBase
{
    private readonly ITrainingService _trainingService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public AdminTrainingsController(
        ITrainingService trainingService,
        IBackgroundJobClient backgroundJobClient)
    {
        _trainingService = trainingService;
        _backgroundJobClient = backgroundJobClient;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TrainingSessionResponse>>> CreateTraining(
        CreateTrainingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var training = await _trainingService.CreateTrainingAsync(request, cancellationToken);

        return Ok(ApiResponse<TrainingSessionResponse>.Success(training, "Trening je uspešno kreiran."));
    }

    [NonAction]
    public ActionResult<ApiResponse<EmptyResponse>> SeedUpcomingTrainings()
    {
        _backgroundJobClient.Enqueue<BiweeklyTrainingSessionSeedingJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        return Ok(ApiResponse<EmptyResponse>.Success(
            EmptyResponse.Value,
            "Dopuna budućih treninga je pokrenuta."));
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
