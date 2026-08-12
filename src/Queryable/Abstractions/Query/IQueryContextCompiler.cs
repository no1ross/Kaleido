using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

/// <summary>
/// Converts a validated QueryRequest into an optimized
/// provider-neutral CompiledRecordQuery.
///
/// Compilation resolves field references, operators,
/// search modes, paging definitions, and named query
/// metadata into runtime structures suitable for execution.
///
/// The resulting compiled query may be reused across
/// multiple providers.
///
/// This interface exists to separate validation from execution.
/// </summary>
internal interface IQueryContextCompiler
{
    CompiledRecordQuery Compile(IQueryRequest request, QueryContextMetadata metadata, QueryViewMetadata queryViewMetadata);
}