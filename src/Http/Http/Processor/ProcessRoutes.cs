namespace Kaleido.Http.Processor;

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
    internal static string ProcessesPrefix(string serviceName, string serviceVersion) =>
        $"{KaleidoHttpRoutePrefix.Service(serviceName, serviceVersion)}/processes";

    public static string ExecuteStep(string serviceName, string serviceVersion, string stepName) =>
        $"{ProcessesPrefix(serviceName, serviceVersion)}/steps/{stepName}";

    public static string Execute(string serviceName, string serviceVersion) =>
        $"{ProcessesPrefix(serviceName, serviceVersion)}/execute";

    public static string ProcessState(string serviceName, string serviceVersion, Guid processId) =>
        $"{ProcessesPrefix(serviceName, serviceVersion)}/{processId}";

    internal static string ProcessStateTemplate(string serviceName, string serviceVersion) =>
        $"{ProcessesPrefix(serviceName, serviceVersion)}/{{processId}}";

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
