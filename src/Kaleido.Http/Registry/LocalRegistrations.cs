using Kaleido.Processor.Registry;
using Kaleido.Queryable.Registry;

namespace Kaleido.Http.Registry;

/// <summary>
/// This service's own registrations in their published (registry) shape: its process steps and its
/// query sources with their views.
/// </summary>
internal sealed class LocalRegistrations(
    IProcessorRegistry processorRegistry,
    IQueryableRegistry queryableRegistry,
    IProcessorResponseFactory responseFactory,
    KaleidoServiceOptions serviceOptions)
{
    public IEnumerable<ProcessorRegistryResponse> Processes() =>
        processorRegistry.Registrations.Select(r => responseFactory.CreateRegistryResponse(r, serviceOptions));

    public IEnumerable<QueryableSourceResponse> Queryables() =>
        queryableRegistry.Registrations.Select(r => QueryableSourceResponse.FromRegistryItem(r, serviceOptions.ServiceName));
}
