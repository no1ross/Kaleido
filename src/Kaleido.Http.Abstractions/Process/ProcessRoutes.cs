namespace Kaleido.Http.Process;

#pragma warning disable KAL0001 // Pure route/endpoint wiring — no state, intentional static

internal static class ProcessRoutePaths
{
    public const string Process =
        "{processId}";

    public const string Execute =
        "execute";

    public const string ProcessTransfer =
        "{processId}/transfer";

    public static string StepMetadata(
        string stepName)
        => $"steps/{stepName}/metadata";

    public static string ExecuteStep(
        string stepName)
        => $"steps/{stepName}";
}

internal static class ProcessContractUrls
{
    internal static string ProcessesPrefix(string serviceName) =>
        string.IsNullOrWhiteSpace(serviceName)
            ? "/processes"
            : $"/{serviceName.Trim().Trim('/')}/processes";

    public static string StepMetadata(string serviceName, string stepName)
        => $"{ProcessesPrefix(serviceName)}/steps/{stepName}/metadata";

    public static string ExecuteStep(string serviceName, string stepName)
        => $"{ProcessesPrefix(serviceName)}/steps/{stepName}";

    public static string Execute(string serviceName)
        => $"{ProcessesPrefix(serviceName)}/execute";

    public static string ProcessTransfer(string serviceName, Guid processId)
        => $"{ProcessesPrefix(serviceName)}/{processId}/transfer";

    public static string ProcessState(string serviceName, Guid processId)
        => $"{ProcessesPrefix(serviceName)}/{processId}";
}

public static class ProcessEndpointNames
{
    public const string ExecuteEndpointName =
        "KaleidoProcessExecute";

    public const string ProcessEndpointName =
        "KaleidoProcessState";

    public const string ProcessTransferEndpointName =
        "KaleidoProcessTransfer";

    public static string StepMetadataEndpointName(
        string stepName) =>
        $"KaleidoProcessStepMetadata_{stepName}";

    public static string StepExecutionEndpointName(
        string stepName) =>
        $"KaleidoProcessStepExecute_{stepName}";
}
