namespace Kaleido.Queryable;

/// <summary>
/// Stable Queryable codes (<c>qry_</c> prefix) for startup registration errors and request
/// validation failures. Startup codes are log-only (<see cref="KaleidoConfigurationException"/>);
/// validation codes are wire-safe and appear in 400 response bodies (<see cref="KaleidoValidationException"/>).
/// </summary>
public static class QueryableErrorCodes
{
    // Startup registration

    /// <summary>A query context or view type is missing a required attribute ([QueryContext] or [QueryView]).</summary>
    public const string MissingAttribute = "qry_missing_attribute";

    /// <summary>A query context has no registered IQueryContextSource or IQueryContextSourceAsync.</summary>
    public const string MissingSource = "qry_missing_source";

    /// <summary>A query context has multiple registered sources.</summary>
    public const string DuplicateSource = "qry_duplicate_source";

    /// <summary>Duplicate query context or view names were detected across the registered assemblies.</summary>
    public const string DuplicateRegistration = "qry_duplicate_registration";

    /// <summary>A query context or view registration is structurally invalid (e.g. bad sort field, unregistered context reference, invalid contract type).</summary>
    public const string InvalidRegistration = "qry_invalid_registration";

    // Request validation

    /// <summary>A field referenced in a filter, sort, or parameter does not exist on the query context.</summary>
    public const string InvalidField = "qry_invalid_field";

    /// <summary>A filter condition uses an operator not supported by the field.</summary>
    public const string UnsupportedOperator = "qry_unsupported_operator";

    /// <summary>A filter condition references a field that is not marked as filterable.</summary>
    public const string FieldNotFilterable = "qry_field_not_filterable";

    /// <summary>A sort clause references a field that is not marked as sortable.</summary>
    public const string FieldNotSortable = "qry_field_not_sortable";

    /// <summary>The requested page size is invalid or exceeds the maximum allowed size.</summary>
    public const string InvalidPageSize = "qry_invalid_page_size";

    /// <summary>The requested page offset is negative.</summary>
    public const string InvalidPageOffset = "qry_invalid_page_offset";

    /// <summary>A search field uses a match mode not supported by the field.</summary>
    public const string UnsupportedMatchMode = "qry_unsupported_match_mode";

    /// <summary>A required named query parameter is missing from the request.</summary>
    public const string MissingParameter = "qry_missing_parameter";

    /// <summary>A named query parameter value has a type incompatible with the declared parameter type.</summary>
    public const string InvalidParameterType = "qry_invalid_parameter_type";

    /// <summary>A search text was provided but no searchable fields are defined on the query context.</summary>
    public const string FieldNotSearchable = "qry_field_not_searchable";

    /// <summary>The same field appears more than once in the sort clause.</summary>
    public const string DuplicateSortField = "qry_duplicate_sort_field";

    /// <summary>A page request was made but the query context does not support paging.</summary>
    public const string PagingNotSupported = "qry_paging_not_supported";

    /// <summary>A filter node is structurally invalid (e.g. both Condition and Group set, or neither).</summary>
    public const string InvalidFilterNode = "qry_invalid_filter_node";

    /// <summary>A search node is structurally invalid.</summary>
    public const string InvalidSearchNode = "qry_invalid_search_node";

    /// <summary>A filter group contains no child expressions.</summary>
    public const string EmptyFilterGroup = "qry_empty_filter_group";

    /// <summary>The filter expression exceeds the maximum allowed nesting depth.</summary>
    public const string FilterDepthExceeded = "qry_filter_depth_exceeded";

    /// <summary>A search group contains no child expressions.</summary>
    public const string EmptySearchGroup = "qry_empty_search_group";

    /// <summary>A filter value has a CLR type not supported by the Queryable transport layer.</summary>
    public const string UnsupportedRuntimeType = "qry_unsupported_runtime_type";

    /// <summary>A filter condition is missing its field name.</summary>
    public const string MissingFilterField = "qry_missing_filter_field";

    /// <summary>A search request is missing the required search text.</summary>
    public const string MissingSearchText = "qry_missing_search_text";

    /// <summary>A filter value cannot be converted to the field's declared type.</summary>
    public const string InvalidFilterValue = "qry_invalid_filter_value";

    /// <summary>A named query parameter value cannot be converted to the parameter's declared type.</summary>
    public const string InvalidParameterValue = "qry_invalid_parameter_value";
}
