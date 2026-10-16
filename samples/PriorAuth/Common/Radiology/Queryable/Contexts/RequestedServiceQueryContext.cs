using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;

public sealed class RequestedServiceQueryContext : IQueryContext
{
    [Key]
    [Description("The requested service on the prior authorization.")]
    public Guid PriorAuthorizationRequestedServiceId { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The prior authorization the service is requested on.")]
    public Guid PriorAuthorizationId { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The process of the prior authorization request.")]
    public Guid ProcessId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Description("The procedure code as the caller entered it.")]
    public string UserEnteredCodeValue { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    [Description("The code system (CPT, HCPCS) of the code the caller entered.")]
    public ProcedureCodeSystem UserEnteredCodeSystem { get; init; }

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    [Description("The procedure code requested after the MRI or CT details refined the entered code.")]
    public string ResolvedCodeValue { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    [Description("The code system of the resolved procedure code.")]
    public ProcedureCodeSystem ResolvedCodeSystem { get; init; }

    [Searchable(Priority = 3, MatchMode = MatchMode.Contains)]
    [Description("The description of the requested service.")]
    public string Description { get; init; } = string.Empty;
}
