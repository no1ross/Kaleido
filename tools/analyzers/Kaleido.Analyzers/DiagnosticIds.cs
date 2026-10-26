namespace Kaleido.Analyzers;

/// <summary>
/// Diagnostic ID constants for the published Kaleido.Analyzers package.
/// KAL2xxx range is reserved for framework-usage rules for consumers.
/// </summary>
internal static class DiagnosticIds
{
    // Attribute validity rules (KAL2001–KAL2003)

    /// <summary>[ProcessStep] DisplayName and Description must be non-empty strings.</summary>
    public const string ProcessStepAttributeValidity = "KAL2001";

    /// <summary>[QuerySource] DisplayName and Description must be non-empty strings.</summary>
    public const string QuerySourceAttributeValidity = "KAL2002";

    /// <summary>[QueryView] DisplayName and Description must be non-empty strings.</summary>
    public const string QueryViewAttributeValidity = "KAL2003";

    /// <summary>Step handler catch (Exception) must filter OperationCanceledException.</summary>
    public const string StepHandlerOceMissing = "KAL2004";

    /// <summary>A constant ServiceName must match [a-z0-9][a-z0-9_-]* (the runtime rule).</summary>
    public const string ServiceNameFormat = "KAL2005";

    /// <summary>AddKaleido() lambda never sets o.Assemblies.</summary>
    public const string AddKaleidoMissingAssemblies = "KAL2006";

    /// <summary>[ProcessStep] applied to a type that does not implement IProcessStep.</summary>
    public const string ProcessStepAttributeWithoutInterface = "KAL2007";

    /// <summary>IProcessStep type is missing the required [ProcessStep] attribute.</summary>
    public const string ProcessStepMissingAttribute = "KAL2008";

    /// <summary>[QuerySource] or [QueryView] applied to a type without the matching interface.</summary>
    public const string QueryableAttributeWithoutInterface = "KAL2009";

    /// <summary>Query source or view is missing its required [QuerySource] or [QueryView] attribute.</summary>
    public const string QueryableMissingAttribute = "KAL2010";

    /// <summary>[Filterable]/[Searchable]/[Sortable] on a property of a type that is not an IQueryContext.</summary>
    public const string QueryRuleAttributeOutsideQueryContext = "KAL2011";

    /// <summary>An IInformationStep declares properties other than InformationRequestId and Items.</summary>
    public const string InformationStepShape = "KAL2012";

    /// <summary>Success&lt;TNext&gt;() names an information step; use RequireInformation&lt;TNext&gt;(request).</summary>
    public const string InformationStepRequiredWithoutRequest = "KAL2013";

    /// <summary>A process step input property has no description.</summary>
    public const string ProcessStepPropertyDescription = "KAL2014";

    /// <summary>A query context or parameters property has no description.</summary>
    public const string QueryPropertyDescription = "KAL2015";

    /// <summary>A step, source or view type name makes an MCP tool name part longer than 40 characters.</summary>
    public const string ToolNameLength = "KAL2016";

    /// <summary>o.ServiceName is longer than 15 characters (the MCP tool-name prefix budget).</summary>
    public const string ServiceNameLength = "KAL2017";

    /// <summary>A [Required] process step input has a non-nullable value type, so it can never be missing.</summary>
    public const string ProcessStepRequiredValueType = "KAL2018";

    /// <summary>A [Required] query parameter has a non-nullable value type, so it can never be missing.</summary>
    public const string QueryParameterRequiredValueType = "KAL2019";

    /// <summary>A [Filterable] operator does not apply to the property's type.</summary>
    public const string FilterOperatorTypeMismatch = "KAL2020";
    public const string OutputPropertyDescription = "KAL2021";
}
