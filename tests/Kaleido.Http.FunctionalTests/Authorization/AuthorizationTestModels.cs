namespace Kaleido.Http.FunctionalTests.Authorization;

internal static class AuthorizationStepNames
{
    public const string InternalStep = nameof(AuthorizedInternalStep);
    public const string PolicyStep = nameof(AuthorizedPolicyStep);
    public const string OpenStep = nameof(AuthorizedOpenStep);
    public const string AnonymousStep = nameof(AuthorizedAnonymousStep);
}

[ProcessStep(DisplayName = AuthorizationStepNames.AnonymousStep, Description = "Anonymous step", Version = "1.0")]
[KaleidoAuthorization(AllowAnonymous = true)]
public sealed record AuthorizedAnonymousStep : IProcessStep;

public sealed class AuthorizedAnonymousStepHandler
    : IProcessStepHandler<AuthorizedAnonymousStep, AuthorizedStepResponse>
{
    public Task<ProcessStepHandlerResult<AuthorizedStepResponse>> ExecuteAsync(
        AuthorizedAnonymousStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            ProcessStepHandlerResult<AuthorizedStepResponse>.Success(
                new AuthorizedStepResponse()));
}

[ProcessStep(DisplayName = AuthorizationStepNames.InternalStep, Description = "Role-secured step", Version = "1.0")]
[KaleidoAuthorization(Roles = "internal")]
public sealed record AuthorizedInternalStep : IProcessStep;

[ProcessStep(DisplayName = AuthorizationStepNames.PolicyStep, Description = "Policy-secured step", Version = "1.0")]
[KaleidoAuthorization(Policy = "clinician-only")]
public sealed record AuthorizedPolicyStep : IProcessStep;

[ProcessStep(DisplayName = AuthorizationStepNames.OpenStep, Description = "Undeclared step", Version = "1.0")]
public sealed record AuthorizedOpenStep : IProcessStep;

public sealed record AuthorizedStepResponse;

public sealed class AuthorizedInternalStepHandler
    : IProcessStepHandler<AuthorizedInternalStep, AuthorizedStepResponse>
{
    public Task<ProcessStepHandlerResult<AuthorizedStepResponse>> ExecuteAsync(
        AuthorizedInternalStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            ProcessStepHandlerResult<AuthorizedStepResponse>.Success(
                new AuthorizedStepResponse()));
}

public sealed class AuthorizedPolicyStepHandler
    : IProcessStepHandler<AuthorizedPolicyStep, AuthorizedStepResponse>
{
    public Task<ProcessStepHandlerResult<AuthorizedStepResponse>> ExecuteAsync(
        AuthorizedPolicyStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            ProcessStepHandlerResult<AuthorizedStepResponse>.Success(
                new AuthorizedStepResponse()));
}

public sealed class AuthorizedOpenStepHandler
    : IProcessStepHandler<AuthorizedOpenStep, AuthorizedStepResponse>
{
    public Task<ProcessStepHandlerResult<AuthorizedStepResponse>> ExecuteAsync(
        AuthorizedOpenStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            ProcessStepHandlerResult<AuthorizedStepResponse>.Success(
                new AuthorizedStepResponse()));
}

public sealed class SecuredRecordContext : IQueryContext
{
    [Filterable(FilterOperator.Equals)]
    [Sortable]
    public int Id { get; init; }
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Secured Records",
    Description = "Role-secured records for authorization tests.")]
[KaleidoAuthorization(Roles = "internal")]
public sealed class SecuredRecordContextSource
    : IQuerySource<SecuredRecordContext>
{
    public IQueryable<SecuredRecordContext> CreateQuery(
        QueryExecutionContext executionContext) =>
        new List<SecuredRecordContext>
        {
            new() { Id = 1 }
        }.AsQueryable();
}

public sealed record SecuredRecordView
{
    public int Id { get; init; }
}

[QueryView(
    DisplayName = "Admin View",
    Description = "Admin-only view over secured records.",
    Version = "1.0.0",
    DefaultSortField = nameof(SecuredRecordContext.Id))]
[KaleidoAuthorization(Roles = "admin")]
public sealed class SecuredAdminView
    : IQueryViewSource<SecuredRecordContextSource, SecuredRecordContext, SecuredRecordView>
{
    public IQueryable<SecuredRecordView> CreateView(
        IQueryable<SecuredRecordContext> query,
        QueryExecutionContext executionContext) =>
        query.Select(r => new SecuredRecordView { Id = r.Id });
}

[QueryView(
    DisplayName = "Internal View",
    Description = "Internal view over secured records.",
    Version = "1.0.0",
    DefaultSortField = nameof(SecuredRecordContext.Id))]
[KaleidoAuthorization(Roles = "internal,admin")]
public sealed class SecuredInternalView
    : IQueryViewSource<SecuredRecordContextSource, SecuredRecordContext, SecuredRecordView>
{
    public IQueryable<SecuredRecordView> CreateView(
        IQueryable<SecuredRecordContext> query,
        QueryExecutionContext executionContext) =>
        query.Select(r => new SecuredRecordView { Id = r.Id });
}
