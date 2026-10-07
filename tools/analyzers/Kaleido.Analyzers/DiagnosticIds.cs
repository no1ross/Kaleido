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

    /// <summary>[QueryContext] Name and Version must be non-empty strings.</summary>
    public const string QueryContextAttributeValidity = "KAL2002";

    /// <summary>[QueryView] Name and Version must be non-empty strings.</summary>
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
}
