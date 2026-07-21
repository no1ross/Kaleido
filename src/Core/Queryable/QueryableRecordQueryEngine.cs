using Kaleido.Metadata;
using Kaleido.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable;

public sealed class QueryableRecordQueryEngine<TRecord> : IRecordQueryEngine<TRecord>
    where TRecord : class
{
    private readonly IRecordMetadataCatalog _metadataCatalog;
    private readonly IRecordQueryValidator _validator;
    private readonly IRecordQueryCompiler _compiler;
    private readonly IQueryableRecordSource<TRecord> _source;
    private readonly IEnumerable<IQueryableRecordNamedQuery<TRecord>> _namedQueries;
    private readonly IQueryableCompiledQueryApplier<TRecord> _applier;
    private readonly IQueryableRecordExecutor<TRecord> _executor;

    public QueryableRecordQueryEngine(
        IRecordMetadataCatalog metadataCatalog,
        IRecordQueryValidator validator,
        IRecordQueryCompiler compiler,
        IQueryableRecordSource<TRecord> source,
        IEnumerable<IQueryableRecordNamedQuery<TRecord>> namedQueries,
        IQueryableCompiledQueryApplier<TRecord> applier,
        IQueryableRecordExecutor<TRecord> executor)
    {
        _metadataCatalog = metadataCatalog;
        _validator = validator;
        _compiler = compiler;
        _source = source;
        _namedQueries = namedQueries;
        _applier = applier;
        _executor = executor;
    }

    public async Task<IRecordQueryResult> ExecuteAsync(KaleidoQueryRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteTypedAsync(request, cancellationToken);
    }

    public async Task<QueryResult<TRecord>> ExecuteTypedAsync(KaleidoQueryRequest request, CancellationToken cancellationToken = default)
    {
        var metadata = _metadataCatalog.GetMetadata<TRecord>();
        _validator.Validate(request, metadata);
        var compiled = _compiler.Compile(request, metadata);
        var query = _source.CreateQuery(new RecordExecutionContext(metadata, request));
        query = ApplyNamedQuery(query, compiled, metadata);
        query = _applier.ApplyFilter(query, compiled.Filter);
        query = _applier.ApplySearch(query, compiled.Search);
        query = _applier.ApplySort(query, compiled.Sort);
        var totalCount = await _executor.CountAsync(query, cancellationToken);
        var nextCursor = compiled.Page.Offset + compiled.Page.Size < totalCount;
        query = _applier.ApplyPage(query, compiled.Page);
        var items = await _executor.ToListAsync(query, cancellationToken);
        return new QueryResult<TRecord>(items, totalCount, metadata);
    }

    private IQueryable<TRecord> ApplyNamedQuery(IQueryable<TRecord> query, CompiledRecordQuery compiled, RuntimeRecordMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(compiled.NamedQuery)) return query;
        var handler = _namedQueries.SingleOrDefault(x => string.Equals(x.Name, compiled.NamedQuery, StringComparison.OrdinalIgnoreCase));
        if (handler is null) throw new InvalidOperationException($"Named query '{compiled.NamedQuery}' is allowed by metadata but no handler is registered for record '{metadata.Name}'.");
        return handler.Apply(query, compiled.Parameters);
    }
}
