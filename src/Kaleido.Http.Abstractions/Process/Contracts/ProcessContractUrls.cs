namespace Kaleido.Http.Process.Contracts;

#pragma warning disable KAL0001 // Pure URL/name factory — no state, intentional static
internal static class ProcessContractUrls
{
    internal static string ProcessesPrefix(string serviceName) =>
        string.IsNullOrWhiteSpace(serviceName)
            ? "/processes"
            : $"/{serviceName.Trim().Trim('/')}/processes";

    public static string Registry(string serviceName)
        => $"{ProcessesPrefix(serviceName)}/registry";

    public static string StepMetadata(string serviceName, string stepName)
        => $"{ProcessesPrefix(serviceName)}/steps/{stepName}/metadata";

    public static string ExecuteStep(string serviceName, string stepName)
        => $"{ProcessesPrefix(serviceName)}/steps/{stepName}";

    public static string Execute(string serviceName)
        => $"{ProcessesPrefix(serviceName)}/execute";

    public static string ProcessState(string serviceName, Guid processId)
        => $"{ProcessesPrefix(serviceName)}/{processId}";
}
