namespace Kaleido.Process.AspNetCore.Contracts;

internal static class ProcessContractUrls
{
    public static string Registry(
        ProcessRouteOptions options)
        => $"{options.ProcessesRoutePrefix}/registry";

    public static string StepMetadata(
        ProcessRouteOptions options,
        string stepName)
        => $"{options.ProcessesRoutePrefix}/steps/{stepName}/metadata";

    public static string ExecuteStep(
        ProcessRouteOptions options,
        string stepName)
        => $"{options.ProcessesRoutePrefix}/steps/{stepName}";
}
