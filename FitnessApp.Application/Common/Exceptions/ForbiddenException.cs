namespace FitnessApp.Application.Common.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
        Errors = Array.Empty<string>();
    }

    public ForbiddenException(string message, IReadOnlyCollection<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyCollection<string> Errors { get; }
}
