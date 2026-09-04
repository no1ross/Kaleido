namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed record QueryableErrorResponse(
    IReadOnlyList<QueryableError> Errors);

public sealed record QueryableError(
    string Code,
    string Message,
    string? Field = null);
