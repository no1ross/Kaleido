using System.ComponentModel;

namespace Kaleido;
/// <summary>Defines the filter operations supported by the framework query model.</summary>
public enum FilterOperator
{
    // Equality
    [Description("eq")]
    Eq,
    [Description("ne")]
    Ne,

    // Comparison
    [Description("gt")]
    Gt,
    [Description("lt")]
    Lt,
    [Description("gte")]
    Gte,
    [Description("lte")]
    Lte,

    // String
    [Description("contains")]
    Contains,
    [Description("notContains")]
    NotContains,
    [Description("startsWith")]
    StartsWith,
    [Description("endsWith")]
    EndsWith,

    // Set
    [Description("in")]
    In,
    [Description("notIn")]
    NotIn,

    [Description("between")]
    Between,
    [Description("notBetween")]
    NotBetween,

    // Null
    [Description("null")]
    IsNull,
    [Description("notNull")]
    IsNotNull,

    // Boolean
    [Description("isTrue")]
    IsTrue,
    [Description("isFalse")]
    IsFalse,

    //// Collection
    //[Description("any")]
    //Any,
    //[Description("all")]
    //All,

    //// Advanced
    //[Description("regex")]
    //Regex,
    //[Description("like")]
    //Like,
    //[Description("notlike")]
    //NotLike
}

public enum MatchMode
{
    [Description("exact")]
    Exact,

    [Description("startsWith")]
    StartsWith,
    [Description("endsWith")]
    EndsWith,
    [Description("contains")]
    Contains,

    [Description("fuzzy")]
    Fuzzy,

    [Description("soundex")]
    Soundex,

    [Description("fullText")]
    FullText
}

public enum SortDirection
{
    [Description("asc")]
    Ascending,
    [Description("desc")]
    Descending
}

public enum LogicalOperator
{
    [Description("and")]
    And,
    [Description("or")]
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
