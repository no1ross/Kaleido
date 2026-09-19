namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when one or more validation errors are detected during Kaleido request or registration processing.
/// </summary>
public sealed class ValidationException
    : Exception
{
    public ValidationException(
        IReadOnlyCollection<ValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyCollection<ValidationError> Errors { get; }
}

/// <summary>A single structured validation error with a stable code and human-readable message.</summary>
public sealed record ValidationError(
    string Code,
    string Message);