using Kaleido.Registry;

namespace Kaleido;

public sealed class KaleidoCatalog : IKaleidoCatalog
{
    private readonly IRecordRegistry _registry;
    private readonly IRecordDispatcher _dispatcher;
    private readonly IRecordDescriptorFactory _descriptors;

    public KaleidoCatalog(IRecordRegistry registry, IRecordDispatcher dispatcher, IRecordDescriptorFactory descriptors)
    {
        _registry = registry;
        _dispatcher = dispatcher;
        _descriptors = descriptors;
    }

    public IReadOnlyCollection<RecordDescriptor> GetAll()
    {
        return _registry.Registrations.Select(x => _descriptors.Create(x.RuntimeMetadata)).ToArray();
    }

    public RecordDescriptor? Get(string recordKey)
    {
        var registration = _registry.Find(recordKey);
        return registration is null ? null : _descriptors.Create(registration.RuntimeMetadata);
    }

    public async Task<KaleidoQueryResponse> QueryAsync(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default)
    {
        return await _dispatcher.DispatchAsync(recordKey, request, cancellationToken);
    }

    public async Task<KaleidoQueryResponse<TRecord>> QueryAsync<TRecord>(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default) where TRecord : class
    {
        return await _dispatcher.DispatchAsync<TRecord>(recordKey, request, cancellationToken);
    }
}
