namespace Kaleido.Samples.PriorAuth.Intake.Process.Models;

public sealed record QueryableErrorResponse(
    IReadOnlyList<QueryableError> Errors);

public sealed record QueryableError(
    string Code,
    string Message,
    string? Field = null);
