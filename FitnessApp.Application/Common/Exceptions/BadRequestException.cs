namespace FitnessApp.Application.Common.Exceptions;

public class BadRequestException : Exception
{
    public BadRequestException(string message)
        : base(message)
    {
        Errors = Array.Empty<string>();
    }

    public BadRequestException(string message, IReadOnlyCollection<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyCollection<string> Errors { get; }
}
