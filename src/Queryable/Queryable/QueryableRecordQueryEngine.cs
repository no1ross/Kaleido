using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Registry;
using Kaleido.Queryable.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Queryable;

public sealed class QueryableRecordQueryEngine<TRecord> : IRecordQueryEngine<TRecord>
    where TRecord : class
{
    private readonly IRecordRegistry _recordRegistry;
    private readonly IRecordQueryValidator _validator;
    private readonly IRecordQueryCompiler _compiler;
    private readonly IQueryableRecordSource<TRecord> _source;
    private readonly IEnumerable<IQueryableRecordNamedQuery<TRecord>> _namedQueries;
    private readonly IQueryableCompiledQueryApplier<TRecord> _applier;
    private readonly IQueryableRecordExecutor<TRecord> _executor;

    public QueryableRecordQueryEngine(
        IRecordRegistry recordRegistry,
        IRecordQueryValidator validator,
        IRecordQueryCompiler compiler,
        IQueryableRecordSource<TRecord> source,
        IEnumerable<IQueryableRecordNamedQuery<TRecord>> namedQueries,
        IQueryableCompiledQueryApplier<TRecord> applier,
        IQueryableRecordExecutor<TRecord> executor)
    {
        _recordRegistry = recordRegistry;
        _validator = validator;
        _compiler = compiler;
        _source = source;
        _namedQueries = namedQueries;
        _applier = applier;
        _executor = executor;
    }

    public async Task<QueryResult<TRecord>> ExecuteAsync(KaleidoQueryRequest request, CancellationToken cancellationToken = default)
    {
        var registration = _recordRegistry.GetRegistration(typeof(TRecord));
        var metadata = registration.Metadata;
        _validator.Validate(request, metadata);
        var compiled = _compiler.Compile(request, metadata);
        var query = _source.CreateQuery(new RecordExecutionContext(metadata, request));
        query = ApplyNamedQuery(query, compiled, metadata);
        query = _applier.ApplyFilter(query, compiled.Filter);
        query = _applier.ApplySearch(query, compiled.Search);
        query = _applier.ApplySort(query, compiled.Sort);
        var totalCount = await _executor.CountAsync(query, cancellationToken);
        query = _applier.ApplyPage(query, compiled.Page);
        var items = await _executor.ToListAsync(query, cancellationToken);
        return new QueryResult<TRecord>(items, totalCount, metadata);
    }

    private IQueryable<TRecord> ApplyNamedQuery(IQueryable<TRecord> query, CompiledRecordQuery compiled, RecordMetadata metadata)
    {
        if (compiled.NamedQuery is null) return query;
        var handler = _namedQueries.SingleOrDefault(x => string.Equals(x.Descriptor.Name, compiled.NamedQuery.Name, StringComparison.OrdinalIgnoreCase));
        if (handler is null) throw new InvalidOperationException($"Named query '{compiled.NamedQuery}' is allowed by metadata but no handler is registered for record '{metadata.Name}'.");
        return handler.Apply(query, compiled.NamedQuery);
    }
}
