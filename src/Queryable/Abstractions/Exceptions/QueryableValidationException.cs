using System.Reflection.Metadata;

namespace Kaleido.Queryable.Exceptions;

public abstract class QueryableValidationException
    : Exception
{
    protected QueryableValidationException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code
    {
        get;
    }
}

public sealed class UnsupportedOperatorException
    : QueryableValidationException
{
    public UnsupportedOperatorException(
        string field,
        FilterOperator op)
        : base(
            QueryErrorCodes.UnsupportedOperator,
            $"Field '{field}' does not support operator '{op}'.")
    {
        Field = field;
        Operator = op;
    }

    public string Field
    {
        get;
    }

    public FilterOperator Operator
    {
        get;
    }
}

public sealed class InvalidFieldException
    : QueryableValidationException
{
    public InvalidFieldException(
        string fieldName,
        string recordName)
        : base(
             QueryErrorCodes.InvalidField,
           $"Field '{fieldName}' does not exist on record '{recordName}'.")
    {
    }
}

public sealed class InvalidPageSizeException
    : QueryableValidationException
{
    public InvalidPageSizeException(
        int requested,
        int max)
        : base(
             QueryErrorCodes.InvalidPageSize,
            $"Page size '{requested}' exceeds maximum page size '{max}'.")
    {
    }
}

public sealed class NamedQueryRequiredException
    : QueryableValidationException
{
    public NamedQueryRequiredException(
        string namedQuery,
        string parameterName)
        : base(
             QueryErrorCodes.NamedQueryNotAllowed,
                        $"Named query '{namedQuery}' requires parameter '{parameterName}'.")
    {
    }
}



public static class QueryErrorCodes
{
    public const string InvalidField =
        "INVALID_FIELD";

    public const string UnsupportedOperator =
        "UNSUPPORTED_OPERATOR";

    public const string FieldNotFilterable =
        "FIELD_NOT_FILTERABLE";

    public const string FieldNotSortable =
        "FIELD_NOT_SORTABLE";

    public const string InvalidPageSize =
        "INVALID_PAGE_SIZE";

    public const string UnsupportedMatchMode =
        "UNSUPPORTED_MATCH_MODE";

    public const string MissingParameter =
        "MISSING_PARAMETER";

    public const string InvalidParameterType =
        "INVALID_PARAMETER_TYPE";

    public const string NamedQueryNotAllowed =
        "NAMED_QUERY_NOT_ALLOWED";

    public const string NamedQueryRequired =
        "NAMED_QUERY_REQUIRED";

    public const string FieldNotSearchable =
        "FIELD_NOT_SEARCHABLE";

    public const string DuplicateSortField =
        "DUPLICATE_SORT_FIELD";

    public const string PagingNotSupported =
        "PAGING_NOT_SUPPORTED";

    public const string InvalidFilterNode =
        "INVALID_FILTER_NODE";

    public const string InvalidSearchNode =
        "INVALID_SEARCH_NODE";

    public const string EmptyFilterGroup =
        "EMPTY_FILTER_GROUP";

    public const string EmptySearchGroup =
        "EMPTY_SEARCH_GROUP";

    public const string UnsupportedRuntimeType =
        "UNSUPPORTED_RUNTIME_TYPE";

    public const string MissingFilterField =
        "MISSING_FILTER_FIELD";

    public const string MissingSearchText =
        "MISSING_SEARCH_TEXT";

    public const string InvalidFilterValue =
        "INVALID_FILTER_VALUE";

    public const string InvalidParameterValue =
        "INVALID_PARAMETER_VALUE";
}





public sealed class FieldNotFilterableException
    : QueryableValidationException
{
    public FieldNotFilterableException(
        string field)
        : base(
            QueryErrorCodes.FieldNotFilterable,
            $"Field '{field}' is not filterable.")
    {
    }
}

public sealed class FieldNotSortableException
    : QueryableValidationException
{
    public FieldNotSortableException(
        string field)
        : base(
            QueryErrorCodes.FieldNotSortable,
            $"Field '{field}' is not sortable.")
    {
    }
}

public sealed class UnsupportedMatchModeException
    : QueryableValidationException
{
    public UnsupportedMatchModeException(
        string field,
        MatchMode matchMode)
        : base(
            QueryErrorCodes.UnsupportedMatchMode,
            $"Field '{field}' does not support match mode '{matchMode}'.")
    {
    }
}

public sealed class DuplicateSortFieldException
    : QueryableValidationException
{
    public DuplicateSortFieldException(
        IReadOnlyList<string> fields)
        : base(
            QueryErrorCodes.DuplicateSortField,
            $"Duplicate sort fields are not allowed: {string.Join(", ", fields)}.")
    {
    }
}

public sealed class PagingNotSupportedException
    : QueryableValidationException
{
    public PagingNotSupportedException(
        string record)
        : base(
            QueryErrorCodes.PagingNotSupported,
            $"Record '{record}' does not support paging.")
    {
    }
}

public sealed class MissingParameterException
    : QueryableValidationException
{
    public MissingParameterException(
        string query,
        string parameter)
        : base(
            QueryErrorCodes.MissingParameter,
            $"Named query '{query}' requires parameter '{parameter}'.")
    {
    }
}

public sealed class InvalidParameterTypeException
    : QueryableValidationException
{
    public InvalidParameterTypeException(
        string parameter,
        Type expected,
        Type actual)
        : base(
            QueryErrorCodes.InvalidParameterType,
            $"Parameter '{parameter}' expects values of type '{expected.Name}' but received '{actual.Name}'.")
    {
    }
}

public sealed class NamedQueryNotAllowedException
    : QueryableValidationException
{
    public NamedQueryNotAllowedException(
        string query,
        string record)
        : base(
            QueryErrorCodes.NamedQueryNotAllowed,
            $"Named query '{query}' is not allowed for record '{record}'.")
    {
    }
}

public sealed class InvalidFilterNodeException
    : QueryableValidationException
{
    public InvalidFilterNodeException(
        string message)
        : base(
            QueryErrorCodes.InvalidFilterNode,
            message)
    {
    }
}

public sealed class EmptyFilterGroupException
    : QueryableValidationException
{
    public EmptyFilterGroupException()
        : base(
            QueryErrorCodes.EmptyFilterGroup,
            "Filter group must contain at least one expression.")
    {
    }
}

public sealed class MissingFilterFieldException
    : QueryableValidationException
{
    public MissingFilterFieldException()
        : base(
            QueryErrorCodes.MissingFilterField,
            "Filter field is required.")
    {
    }
}

public sealed class FieldNotSearchableException
    : QueryableValidationException
{
    public FieldNotSearchableException(
        string field)
        : base(
            QueryErrorCodes.FieldNotSearchable,
            $"Field '{field}' is not searchable.")
    {
    }
}

public sealed class InvalidSearchNodeException
    : QueryableValidationException
{
    public InvalidSearchNodeException(
        string message)
        : base(
            QueryErrorCodes.InvalidSearchNode,
            message)
    {
    }
}

public sealed class EmptySearchGroupException
    : QueryableValidationException
{
    public EmptySearchGroupException()
        : base(
            QueryErrorCodes.EmptySearchGroup,
            "Search group must contain at least one expression.")
    {
    }
}

public sealed class MissingSearchTextException
    : QueryableValidationException
{
    public MissingSearchTextException()
        : base(
            QueryErrorCodes.MissingSearchText,
            "Search text is required.")
    {
    }
}

public sealed class UnsupportedRuntimeTypeException
    : QueryableValidationException
{
    public UnsupportedRuntimeTypeException(
        string name,
        Type actualType)
        : base(
            QueryErrorCodes.UnsupportedRuntimeType,
            $"Value '{name}' contains unsupported runtime type '{actualType.FullName}'. " +
            "Transport layers must normalize values before invoking Queryable.")
    {
        Name = name;
        ActualType = actualType;
    }

    public string Name
    {
        get;
    }

    public Type ActualType
    {
        get;
    }
}

public sealed class InvalidFilterValueException
    : QueryableValidationException
{
    public InvalidFilterValueException(
        string field,
        object? value,
        Type expectedType)
        : base(
            QueryErrorCodes.InvalidFilterValue,
            $"Value '{value}' is not valid for field '{field}'. Expected a value of type '{expectedType.Name}'.")
    {
    }
}

public sealed class ValueConversionException
    : QueryableValidationException
{
    public ValueConversionException(
        string name,
        object? value,
        Type expectedType)
        : base(
            QueryErrorCodes.InvalidParameterType,
            $"Value '{value}' is not valid for parameter '{name}'. Expected a value of type '{expectedType.Name}'.")
    {
    }
}

public sealed class InvalidParameterValueException
    : QueryableValidationException
{
    public InvalidParameterValueException(
        string parameter,
        object? value,
        Type expectedType)
        : base(
            QueryErrorCodes.InvalidParameterValue,
            $"Value '{value}' is not valid for parameter '{parameter}'. Expected a value of type '{expectedType.Name}'.")
    {
    }
}