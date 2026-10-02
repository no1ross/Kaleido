using System.ComponentModel.DataAnnotations;

namespace Kaleido.AspNetCore.FunctionalTests.Authorization;

internal static class AuthorizationStepNames
{
    public const string InternalStep = "auth-internal";
    public const string PolicyStep = "auth-policy";
    public const string OpenStep = "auth-open";
}

[ProcessStep(Name = AuthorizationStepNames.InternalStep, Description = "Role-secured step", Version = "1.0")]
[KaleidoAuthorization(Roles = "internal")]
public sealed record AuthorizedInternalStep;

[ProcessStep(Name = AuthorizationStepNames.PolicyStep, Description = "Policy-secured step", Version = "1.0")]
[KaleidoAuthorization(Policy = "clinician-only")]
public sealed record AuthorizedPolicyStep;

[ProcessStep(Name = AuthorizationStepNames.OpenStep, Description = "Undeclared step", Version = "1.0")]
public sealed record AuthorizedOpenStep;

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

[QueryContext(
    Name = "secured-records",
    DisplayName = "Secured Records",
    Description = "Role-secured records for authorization tests.",
    Version = "1.0.0",
    Kind = QueryContextKind.Direct)]
[KaleidoAuthorization(Roles = "internal")]
public sealed class SecuredRecordContext
{
    [Filterable(FilterOperator.Equals)]
    [Sortable]
    public int Id { get; init; }
}

public sealed class SecuredRecordContextSource
    : IQueryContextSource<SecuredRecordContext>
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
    Name = "admin-view",
    DisplayName = "Admin View",
    Description = "Admin-only view over secured records.",
    Version = "1.0.0",
    DefaultSortField = nameof(SecuredRecordContext.Id))]
[KaleidoAuthorization(Roles = "admin")]
public sealed class SecuredAdminView
    : IQueryViewSource<SecuredRecordContext, SecuredRecordView>
{
    public IQueryable<SecuredRecordView> CreateView(
        IQueryable<SecuredRecordContext> query,
        QueryExecutionContext executionContext) =>
        query.Select(r => new SecuredRecordView { Id = r.Id });
}

[QueryView(
    Name = "internal-view",
    DisplayName = "Internal View",
    Description = "Internal view over secured records.",
    Version = "1.0.0",
    DefaultSortField = nameof(SecuredRecordContext.Id))]
[KaleidoAuthorization(Roles = "internal,admin")]
public sealed class SecuredInternalView
    : IQueryViewSource<SecuredRecordContext, SecuredRecordView>
{
    public IQueryable<SecuredRecordView> CreateView(
        IQueryable<SecuredRecordContext> query,
        QueryExecutionContext executionContext) =>
        query.Select(r => new SecuredRecordView { Id = r.Id });
}
