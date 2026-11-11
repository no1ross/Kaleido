using Kaleido.Registry;

namespace Kaleido.Queryable.Metadata;

/// <summary>A discovered local query view and its metadata.</summary>
/// <param name="QueryViewType">The view type (the capability's identity).</param>
/// <param name="ViewType">The record type the view returns.</param>
/// <param name="ViewParametersType">The view's parameters record.</param>
/// <param name="SourceType">The local source the view projects.</param>
/// <param name="QueryContextType">The source's query context record.</param>
/// <param name="Metadata">The view's published metadata.</param>
[ExcludeFromCodeCoverage]
internal sealed record QueryViewRegistration
(
    Type QueryViewType,
    Type ViewType,
    Type ViewParametersType,
    Type SourceType,
    Type QueryContextType,
    QueryViewMetadata Metadata
);

/// <summary>Published metadata for a local query view.</summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryViewMetadata
(
    string Name,
    string DisplayName,
    string Description,
    PageableMetadata? Pageable,
    IReadOnlyList<QueryParameterMetadata>? Parameters,
    IReadOnlyList<QueryOutputFieldMetadata>? OutputFields,
    AuthorizationMetadata? Authorization = null
);
