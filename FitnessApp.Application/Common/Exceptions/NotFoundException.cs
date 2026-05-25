namespace FitnessApp.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
        Errors = Array.Empty<string>();
    }

    public NotFoundException(string message, IReadOnlyCollection<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyCollection<string> Errors { get; }
}
