namespace Kaleido.Process.AspNetCore.Contracts;

public static class ProcessEndpointNames
{
    public const string ParticipantCatalogEndpointName =
        "KaleidoProcessCatalog";

    public const string ExecuteEndpointName =
        "KaleidoProcessExecute";

    public const string ProcessEndpointName =
        "KaleidoProcessState";

    public const string StepCatalogEndpointName =
        "KaleidoProcessStepCatalog";

    public static string StepMetadataEndpointName(
        string stepName) =>
        $"KaleidoProcessStepMetadata_{stepName}";

    public static string StepExecutionEndpointName(
        string stepName) =>
        $"KaleidoProcessStepExecute_{stepName}";
}