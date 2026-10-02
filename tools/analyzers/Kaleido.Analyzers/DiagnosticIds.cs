namespace Kaleido.Analyzers;

/// <summary>
/// Diagnostic ID constants for the published Kaleido.Analyzers package.
/// KAL2xxx range is reserved for framework-usage rules for consumers.
/// </summary>
internal static class DiagnosticIds
{
    // Attribute validity rules (KAL2001–KAL2003)

    /// <summary>[ProcessStep] Name and Version must be non-empty strings.</summary>
    public const string ProcessStepAttributeValidity = "KAL2001";

    /// <summary>[QueryContext] Name and Version must be non-empty strings.</summary>
    public const string QueryContextAttributeValidity = "KAL2002";

    /// <summary>[QueryView] Name and Version must be non-empty strings.</summary>
    public const string QueryViewAttributeValidity = "KAL2003";

    /// <summary>Step handler catch (Exception) must filter OperationCanceledException.</summary>
    public const string StepHandlerOceMissing = "KAL2004";

    /// <summary>o.ServiceName must be lowercase with no spaces or separators.</summary>
    public const string ServiceNameFormat = "KAL2005";

    /// <summary>[ProcessStep] class name must end in 'Step'.</summary>
    public const string ProcessStepSuffix = "KAL2007";

    /// <summary>[ProcessStep] class has no IProcessStepHandler in same compilation.</summary>
    public const string ProcessStepMissingHandler = "KAL2008";

    /// <summary>AddKaleido() lambda never sets o.Assemblies.</summary>
    public const string AddKaleidoMissingAssemblies = "KAL2009";
}
