using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable;

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
public interface IRecordQueryCompiler
{
    CompiledRecordQuery Compile(KaleidoQueryRequest request, RecordMetadata metadata);
}