namespace FitnessApp.Application.Common.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
        Errors = Array.Empty<string>();
    }

    public ConflictException(string message, IReadOnlyCollection<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyCollection<string> Errors { get; }
}
