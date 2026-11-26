namespace Kaleido.Process.Http;

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
