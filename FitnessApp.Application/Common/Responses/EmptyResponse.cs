namespace FitnessApp.Application.Common.Responses;

public sealed record EmptyResponse
{
    public static EmptyResponse Value { get; } = new();

    private EmptyResponse()
    {
    }
}
