using System.ComponentModel;

namespace Kaleido.Queryable;
/// <summary>Defines the filter operations supported by the framework query model.</summary>
public enum FilterOperator
{
    // Equality
    Equals,
    NotEquals,

    // Comparison
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,

    // String
    Contains,
    NotContains,
    StartsWith,
    EndsWith,

    // Set
    In,
    NotIn,

    Between,
    NotBetween,

    // Null
    IsNull,
    IsNotNull,

    // Boolean
    IsTrue,
    IsFalse,

    //// Collection
    //Any,
    //All,

    //// Advanced
    //Regex,
    //Like,
    //NotLike
}

public enum MatchMode
{
    Exact,
    StartsWith,
    EndsWith,
    Contains
}

public enum SortDirection
{
    Ascending,
    Descending
}

public enum LogicalOperator
{
    And,
    Or
}

public enum ValueDataType 
{ 
    String, 
    Integer,
    Long,
    Decimal, 
    Boolean, 
    Guid,
    Date,
    DateTime,
    Time,
    Enum, 
    Unknown
}
