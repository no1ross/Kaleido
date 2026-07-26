using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Provides endpoint registration extensions for Kaleido Queryable.
/// </summary>
public static class QueryableEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapQueryable(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var registry =
            endpoints.ServiceProvider
                .GetRequiredService<IRecordRegistry>();

        var options =
            endpoints.ServiceProvider
                .GetRequiredService<IOptions<QueryableAspNetCoreOptions>>()
                .Value;

        var group =
            endpoints.MapGroup(options.RoutePrefix);

        group.MapGet(
                "",
                () => Results.Ok(
                    registry.Registrations
                        .Select(r =>
                            RecordContract.ToSummary(
                                r,
                                options))
                        .OrderBy(r => r.Name)))
            .WithName(QueryableEndpointNames.CatalogEndpointName)
            .WithTags("Queryable");

        foreach (var record in registry.Registrations)
        {
            group.MapRecord(
                record,
                options);
        }

        return endpoints;
    }
}
