namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when a Kaleido request fails validation — bad field, unsupported operator, invalid page size, etc.
/// Results in a 400 Bad Request when caught by the endpoint handlers.
/// The <see cref="Code"/> is forwarded directly into the HTTP response body (see <see cref="ValidationErrorCodes"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KaleidoValidationException : Exception
{
    public KaleidoValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public KaleidoValidationException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable error code (see <see cref="ValidationErrorCodes"/>). Wire-safe — forwarded directly into 400 response bodies.</summary>
    public string Code { get; }
}

/// <summary>
/// Stable machine-readable error codes for Kaleido validation failures.
/// These codes are wire-safe — they appear directly in 400 response bodies.
/// Queryable codes are prefixed <c>qry_</c>; Process codes will use <c>pro_</c> when added.
/// </summary>
public static class ValidationErrorCodes
{
    // Queryable validation (qry_ prefix)

    /// <summary>A field referenced in a filter, sort, or parameter does not exist on the query context.</summary>
    public const string QryInvalidField           = "qry_invalid_field";

    /// <summary>A filter condition uses an operator not supported by the field.</summary>
    public const string QryUnsupportedOperator    = "qry_unsupported_operator";

    /// <summary>A filter condition references a field that is not marked as filterable.</summary>
    public const string QryFieldNotFilterable     = "qry_field_not_filterable";

    /// <summary>A sort clause references a field that is not marked as sortable.</summary>
    public const string QryFieldNotSortable       = "qry_field_not_sortable";

    /// <summary>The requested page size is invalid or exceeds the maximum allowed size.</summary>
    public const string QryInvalidPageSize        = "qry_invalid_page_size";

    /// <summary>A search field uses a match mode not supported by the field.</summary>
    public const string QryUnsupportedMatchMode   = "qry_unsupported_match_mode";

    /// <summary>A required named query parameter is missing from the request.</summary>
    public const string QryMissingParameter       = "qry_missing_parameter";

    /// <summary>A named query parameter value has a type incompatible with the declared parameter type.</summary>
    public const string QryInvalidParameterType   = "qry_invalid_parameter_type";

    /// <summary>A search text was provided but no searchable fields are defined on the query context.</summary>
    public const string QryFieldNotSearchable     = "qry_field_not_searchable";

    /// <summary>The same field appears more than once in the sort clause.</summary>
    public const string QryDuplicateSortField     = "qry_duplicate_sort_field";

    /// <summary>A page request was made but the query context does not support paging.</summary>
    public const string QryPagingNotSupported     = "qry_paging_not_supported";

    /// <summary>A filter node is structurally invalid (e.g. both Condition and Group set, or neither).</summary>
    public const string QryInvalidFilterNode      = "qry_invalid_filter_node";

    /// <summary>A search node is structurally invalid.</summary>
    public const string QryInvalidSearchNode      = "qry_invalid_search_node";

    /// <summary>A filter group contains no child expressions.</summary>
    public const string QryEmptyFilterGroup       = "qry_empty_filter_group";

    /// <summary>The filter expression exceeds the maximum allowed nesting depth.</summary>
    public const string QryFilterDepthExceeded    = "qry_filter_depth_exceeded";

    /// <summary>A search group contains no child expressions.</summary>
    public const string QryEmptySearchGroup       = "qry_empty_search_group";

    /// <summary>A filter value has a CLR type not supported by the Queryable transport layer.</summary>
    public const string QryUnsupportedRuntimeType = "qry_unsupported_runtime_type";

    /// <summary>A filter condition is missing its field name.</summary>
    public const string QryMissingFilterField     = "qry_missing_filter_field";

    /// <summary>A search request is missing the required search text.</summary>
    public const string QryMissingSearchText      = "qry_missing_search_text";

    /// <summary>A filter value cannot be converted to the field's declared type.</summary>
    public const string QryInvalidFilterValue     = "qry_invalid_filter_value";

    /// <summary>A named query parameter value cannot be converted to the parameter's declared type.</summary>
    public const string QryInvalidParameterValue  = "qry_invalid_parameter_value";

    // Process validation — reserved for future use (pro_ prefix when added)
}
