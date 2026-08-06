namespace Kaleido.Process.AspNetCore.Contracts;

internal static class ProcessContractUrls
{
    public static string StepMetadata(
        ProcessRouteOptions options,
        string stepName)
        => $"{options.RoutePrefix.TrimEnd('/')}/steps/{stepName}";

    public static string ExecuteStep(
        ProcessRouteOptions options,
        string stepName)
        => $"{options.RoutePrefix.TrimEnd('/')}/steps/{stepName}";
}
