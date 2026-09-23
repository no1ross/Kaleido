namespace Kaleido.Http.Process.Contracts;

#pragma warning disable KAL0001 // Pure URL/name factory — no state, intentional static
public static class ProcessEndpointNames
{
    public const string ProcessorCatalogEndpointName =
        "KaleidoProcessCatalog";

    public const string ExecuteEndpointName =
        "KaleidoProcessExecute";

    public const string ProcessEndpointName =
        "KaleidoProcessState";

    public const string StepCatalogEndpointName =
        "KaleidoProcessStepCatalog";

    public const string StepRegistryEndpointName =
        "KaleidoProcessStepREgistry";

    public static string StepMetadataEndpointName(
        string stepName) =>
        $"KaleidoProcessStepMetadata_{stepName}";

    public static string StepExecutionEndpointName(
        string stepName) =>
        $"KaleidoProcessStepExecute_{stepName}";
}
