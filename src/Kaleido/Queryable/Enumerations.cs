using System.ComponentModel;

namespace Kaleido.Queryable;
/// <summary>Defines the filter operations supported by the framework query model.</summary>
public enum FilterOperator
{
    // Equality
    /// <summary>
    /// The field equals the first value. On a string field the comparison is case-insensitive and
    /// a null field never matches a non-null value; a null value matches a null field.
    /// </summary>
    [Description("Equals")]
    Equals,
    /// <summary>
    /// The negation of <c>Equals</c>: the field does not equal the first value. On a string
    /// field the comparison is case-insensitive and a null field matches any non-null value.
    /// </summary>
    [Description("Not Equals")]
    NotEquals,

    // Comparison
    /// <summary>The field is greater than the first value.</summary>
    [Description("Greater Than")]
    GreaterThan,
    /// <summary>The field is less than the first value.</summary>
    [Description("Less Than")]
    LessThan,
    /// <summary>The field is greater than or equal to the first value.</summary>
    [Description("Greater Than Or Equal")]
    GreaterThanOrEqual,
    /// <summary>The field is less than or equal to the first value.</summary>
    [Description("Less Than Or Equal")]
    LessThanOrEqual,

    // String
    /// <summary>
    /// The string field contains the first value, case-insensitively. A null field never matches.
    /// String fields only.
    /// </summary>
    [Description("Contains")]
    Contains,
    /// <summary>
    /// The negation of <see cref="Contains"/>: the string field does not contain the first value,
    /// case-insensitively. A null field matches. String fields only.
    /// </summary>
    [Description("Not Contains")]
    NotContains,
    /// <summary>
    /// The string field starts with the first value, case-insensitively. A null field never
    /// matches. String fields only.
    /// </summary>
    [Description("Starts With")]
    StartsWith,
    /// <summary>
    /// The string field ends with the first value, case-insensitively. A null field never matches.
    /// String fields only.
    /// </summary>
    [Description("Ends With")]
    EndsWith,

    // Set
    /// <summary>
    /// The field equals any of the values. On a string field the comparison is case-insensitive
    /// and a null field never matches. With no values nothing matches.
    /// </summary>
    [Description("In")]
    In,
    /// <summary>
    /// The negation of <see cref="In"/>: the field equals none of the values. On a string field a
    /// null field matches. With no values everything matches.
    /// </summary>
    [Description("Not In")]
    NotIn,

    /// <summary>
    /// The field lies between the first value (lower bound) and the second (upper bound), both
    /// inclusive. Fewer than two values fails with <c>QueryableErrorCodes.InvalidFilterNode</c>.
    /// </summary>
    [Description("Between")]
    Between,
    /// <summary>
    /// The negation of <see cref="Between"/>: the field is below the first value or above the
    /// second. Fewer than two values fails with <c>QueryableErrorCodes.InvalidFilterNode</c>.
    /// </summary>
    [Description("Not Between")]
    NotBetween,

    // Null
    /// <summary>The field is null. Takes no values.</summary>
    [Description("Is Null")]
    IsNull,
    /// <summary>The field is not null. Takes no values.</summary>
    [Description("Is Not Null")]
    IsNotNull,

    // Boolean
    /// <summary>
    /// The <see cref="bool"/> or nullable <see cref="bool"/> field is <see langword="true"/>; a null
    /// field does not match. Takes no values.
    /// </summary>
    [Description("Is True")]
    IsTrue,
    /// <summary>
    /// The <see cref="bool"/> or nullable <see cref="bool"/> field is <see langword="false"/>; a null
    /// field does not match. Takes no values.
    /// </summary>
    [Description("Is False")]
    IsFalse,
}

/// <summary>
/// How free-text search text is matched against a searchable field, declared per field with
/// <see cref="SearchableAttribute.MatchMode"/>.
/// </summary>
/// <remarks>
/// Matching against a string field is case-insensitive and a null field never matches. A record
/// matches the search when any of its searchable fields matches.
/// </remarks>
public enum MatchMode
{
    /// <summary>The field equals the search text.</summary>
    [Description("Exact Match")]
    Exact,
    /// <summary>The field starts with the search text. String fields only.</summary>
    [Description("Starts With")]
    StartsWith,
    /// <summary>The field ends with the search text. String fields only.</summary>
    [Description("Ends With")]
    EndsWith,
    /// <summary>The field contains the search text. String fields only.</summary>
    [Description("Contains")]
    Contains,
}

/// <summary>The direction of one key of a query's sort order.</summary>
public enum SortDirection
{
    /// <summary>Smallest value first.</summary>
    [Description("Ascending")]
    Ascending,
    /// <summary>Largest value first.</summary>
    [Description("Descending")]
    Descending,
}

/// <summary>How the child nodes of a filter group are combined.</summary>
public enum LogicalOperator
{
    /// <summary>The group matches when every child matches.</summary>
    [Description("And")]
    And,
    /// <summary>The group matches when any child matches.</summary>
    [Description("Or")]
    Or,
}
