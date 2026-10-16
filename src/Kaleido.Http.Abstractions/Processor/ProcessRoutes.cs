namespace Kaleido.Http.Processor;

#pragma warning disable KAL0001 // Pure route/endpoint wiring — no state, intentional static

internal static class ProcessRoutePaths
{
    public const string Process =
        "{processId}";

    public const string Execute =
        "execute";

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

    public static string ExecuteStep(string serviceName, string stepName)
        => $"{ProcessesPrefix(serviceName)}/steps/{stepName}";

    public static string Execute(string serviceName)
        => $"{ProcessesPrefix(serviceName)}/execute";

    public static string ProcessState(string serviceName, Guid processId)
        => $"{ProcessesPrefix(serviceName)}/{processId}";
}

internal static class ProcessEndpointNames
{
    public const string ExecuteEndpointName =
        "KaleidoProcessExecute";

    public const string ProcessEndpointName =
        "KaleidoProcessState";

    public static string StepExecutionEndpointName(
        string stepName) =>
        $"KaleidoProcessStepExecute_{stepName}";
}
