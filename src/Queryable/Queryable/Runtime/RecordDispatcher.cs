using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;

internal sealed class RecordDispatcher : IRecordDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRecordRegistry _registry;

    public RecordDispatcher(IServiceScopeFactory scopeFactory, IRecordRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(registry);

        _scopeFactory = scopeFactory;
        _registry = registry;
    }

    public async Task<QueryResult<TRecord>> DispatchAsync<TRecord>(
        string recordKey,
        QueryRequest request,
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
            await engine.ExecuteAsync(
                request,
                cancellationToken);

        return result;
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