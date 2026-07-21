using Kaleido;
using Kaleido.Metadata;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;

public sealed class RecordDispatcher : IRecordDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRecordRegistry _registry;
    private readonly IRecordDescriptorFactory _descriptors;

    public RecordDispatcher(
        IServiceScopeFactory scopeFactory,
        IRecordRegistry registry,
        IRecordDescriptorFactory descriptors)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _descriptors = descriptors;
    }

    public async Task<KaleidoQueryResponse> DispatchAsync(
        string recordKey,
        KaleidoQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var registration =
            GetRegistration(recordKey);

        using var scope =
            _scopeFactory.CreateScope();

        var engineType =
            typeof(IRecordQueryEngine<>)
                .MakeGenericType(registration.RecordType);

        var engine =
            (IRecordQueryEngine)
            scope.ServiceProvider.GetRequiredService(
                engineType);

        var result =
            await engine.ExecuteAsync(
                request,
                cancellationToken);

        return new KaleidoQueryResponse(
            _descriptors.Create(
                result.RuntimeMetadata),
            result.TotalCount,
            result.ItemsAsObjects);
    }

    public async Task<KaleidoQueryResponse<TRecord>> DispatchAsync<TRecord>(
        string recordKey,
        KaleidoQueryRequest request,
        CancellationToken cancellationToken = default)
        where TRecord : class
    {
        var registration =
            GetRegistration(recordKey);

        ValidateRecordType<TRecord>(
            recordKey,
            registration);

        using var scope =
            _scopeFactory.CreateScope();

        var engine =
            scope.ServiceProvider
                .GetRequiredService<IRecordQueryEngine<TRecord>>();

        var result =
            await engine.ExecuteTypedAsync(
                request,
                cancellationToken);

        return new KaleidoQueryResponse<TRecord>(
            _descriptors.Create(
                result.RuntimeMetadata),
            result.TotalCount,
            result.Items);
    }

    private RecordRegistration GetRegistration(
        string recordKey)
    {
        return _registry.Find(recordKey)
            ?? throw new KeyNotFoundException(
                $"Record '{recordKey}' is not registered.");
    }

    private static void ValidateRecordType<TRecord>(
        string recordKey,
        RecordRegistration registration)
    {
        if (registration.RecordType != typeof(TRecord))
        {
            throw new InvalidOperationException(
                $"Record '{recordKey}' is registered for '{registration.RecordType.Name}' " +
                $"but was requested as '{typeof(TRecord).Name}'.");
        }
    }
}