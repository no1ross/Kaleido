using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable;
/// <summary>Defines the filter operations supported by the framework query model.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterOperator
{
    // Equality
    [Description("Equals")]
    Equals,
    [Description("Not Equals")]
    NotEquals,

    // Comparison
    [Description("Greater Than")]
    GreaterThan,
    [Description("Less Than")]
    LessThan,
    [Description("Greater Than Or Equal")]
    GreaterThanOrEqual,
    [Description("Less Than Or Equal")] 
    LessThanOrEqual,

    // String
    [Description("Contains")]
    Contains,
    [Description("Not Contains")]
    NotContains,
    [Description("Starts With")]
    StartsWith,
    [Description("Ends With")]
    EndsWith,

    // Set
    [Description("In")]
    In,
    [Description("Not In")]
    NotIn,

    [Description("Between")]
    Between,
    [Description("Not Between")]
    NotBetween,

    // Null
    [Description("Is Null")]
    IsNull,
    [Description("Is Not Null")]
    IsNotNull,

    // Boolean
    [Description("Is True")]
    IsTrue,
    [Description("Is False")]
    IsFalse,

    //// Collection
    //Any,
    //All,

    //// Advanced
    //Regex,
    //Like,
    //NotLike
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MatchMode
{
    [Description("Exact Match")]
    Exact,
    [Description("Starts With")]
    StartsWith,
    [Description("Ends With")]
    EndsWith,
    [Description("Contains")]
    Contains
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    [Description("Ascending")]
    Ascending,
    [Description("Descending")]
    Descending
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogicalOperator
{
    [Description("And")]
    And,
    [Description("Or")]
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
