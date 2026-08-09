namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryErrorResponse(
    IReadOnlyList<QueryError> Errors);

public sealed record QueryError(
    string Code,
    string Message,
    string? Field = null);