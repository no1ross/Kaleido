using Kaleido.Platform.Registry;
using Kaleido.Processor.Registry;
using Kaleido.Queryable.Registry;

namespace Kaleido.Http.Server.Registry;

/// <summary>
/// This service's own registrations in their published (registry) shape: its process steps and its
/// query sources with their views.
/// </summary>
/// <remarks>
/// A host may have no capabilities of one kind, or none at all (a router only aggregates), so the
/// Process and Queryable registries are optional: a missing one contributes nothing.
/// </remarks>
internal sealed class LocalRegistrations(
    IEnumerable<IProcessorRegistry> processorRegistries,
    IEnumerable<IQueryableRegistry> queryableRegistries,
    IProcessorResponseFactory responseFactory,
    KaleidoServiceOptions serviceOptions)
    : IKaleidoLocalCapabilities
{
    // What this host registers with the control registry (#553).
    public KaleidoRegistry Capabilities() =>
        new() { Processes = [.. Processes()], Queryables = [.. Queryables()] };

    public IEnumerable<ProcessorRegistryResponse> Processes() =>
        processorRegistries
            .Take(1)
            .SelectMany(x => x.Registrations)
            .Select(responseFactory.CreateRegistryResponse);

    public IEnumerable<QueryableSourceResponse> Queryables() =>
        queryableRegistries
            .Take(1)
            .SelectMany(x => x.Registrations)
            .Select(r => r.ToResponse(serviceOptions));
}
