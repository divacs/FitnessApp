namespace FitnessApp.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
        Errors = Array.Empty<string>();
    }

    public ValidationException(string message, IReadOnlyCollection<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyCollection<string> Errors { get; }
}
