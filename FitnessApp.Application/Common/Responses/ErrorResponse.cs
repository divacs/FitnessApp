namespace FitnessApp.Application.Common.Responses;

public class ErrorResponse
{
    public string Message { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Errors { get; init; } = Array.Empty<string>();
}
