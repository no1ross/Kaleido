namespace Kaleido.Analyzers;

/// <summary>
/// Diagnostic ID constants for the published Kaleido.Analyzers package.
/// KAL2xxx range is reserved for framework-usage rules for consumers.
/// </summary>
internal static class DiagnosticIds
{
    // Attribute validity rules (KAL2001–KAL2003)

    /// <summary>[ProcessStep] Version, DisplayName, and Description must be non-empty strings.</summary>
    public const string ProcessStepAttributeValidity = "KAL2001";

    /// <summary>[QuerySource] Version, DisplayName, and Description must be non-empty strings.</summary>
    public const string QuerySourceAttributeValidity = "KAL2002";

    /// <summary>[QueryView] Version, DisplayName, and Description must be non-empty strings.</summary>
    public const string QueryViewAttributeValidity = "KAL2003";

    /// <summary>Step handler catch (Exception) must filter OperationCanceledException.</summary>
    public const string StepHandlerOceMissing = "KAL2004";

    /// <summary>o.ServiceName must be lowercase with no spaces or separators.</summary>
    public const string ServiceNameFormat = "KAL2005";

    /// <summary>IProcessStep type has no IProcessStepHandler in same compilation.</summary>
    public const string ProcessStepMissingHandler = "KAL2008";

    /// <summary>AddKaleido() lambda never sets o.Assemblies.</summary>
    public const string AddKaleidoMissingAssemblies = "KAL2009";

    /// <summary>[ProcessStep] applied to a type that does not implement IProcessStep.</summary>
    public const string ProcessStepAttributeWithoutInterface = "KAL2010";

    /// <summary>IProcessStep type is missing the required [ProcessStep] attribute.</summary>
    public const string ProcessStepMissingAttribute = "KAL2011";

    /// <summary>[QuerySource] or [QueryView] applied to a type without the matching interface.</summary>
    public const string QueryableAttributeWithoutInterface = "KAL2012";

    /// <summary>Query source or view is missing its required [QuerySource] or [QueryView] attribute.</summary>
    public const string QueryableMissingAttribute = "KAL2013";

    /// <summary>[Filterable]/[Searchable]/[Sortable] on a property of a type that is not an IQueryContext.</summary>
    public const string QueryRuleAttributeOutsideQueryContext = "KAL2014";

    /// <summary>An IInformationStep declares properties other than InformationRequestId and Items.</summary>
    public const string InformationStepShape = "KAL2015";

    /// <summary>Success&lt;TNext&gt;() names an information step; use RequireInformation&lt;TNext&gt;(request).</summary>
    public const string InformationStepRequiredWithoutRequest = "KAL2016";

    /// <summary>A process step input property has no description.</summary>
    public const string ProcessStepPropertyDescription = "KAL2017";

    /// <summary>A query context or parameters property has no description.</summary>
    public const string QueryPropertyDescription = "KAL2018";
}
