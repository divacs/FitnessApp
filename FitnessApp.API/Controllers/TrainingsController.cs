using FitnessApp.Application.Common.Responses;
using FitnessApp.Application.Features.Trainings.DTOs;
using FitnessApp.Application.Features.Trainings.Interfaces;
using FitnessApp.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessApp.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyConstants.VerifiedUsersOnly)]
[Route("api/trainings")]
public class TrainingsController : ControllerBase
{
    private readonly ITrainingService _trainingService;

    public TrainingsController(ITrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TrainingCalendarResponse>>>> GetTrainings(
        [FromQuery] DateTime? date = null,
        [FromQuery] bool? isCancelled = false,
        CancellationToken cancellationToken = default)
    {
        var trainings = await _trainingService.GetUpcomingTrainingsAsync(
            date,
            isCancelled,
            cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<TrainingCalendarResponse>>.Success(trainings));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TrainingSessionResponse>>> GetTraining(
        Guid id,
        CancellationToken cancellationToken)
    {
        var training = await _trainingService.GetTrainingByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<TrainingSessionResponse>.Success(training));
    }
}
