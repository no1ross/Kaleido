namespace Kaleido.Process.AspNetCore.Contracts;

internal static class ProcessContractUrls
{
    public static string StepMetadata(
        ProcessRouteOptions options,
        string stepName)
        => $"{options.ProcessesRoutePrefix}/steps/{stepName}/metadata";

    public static string ExecuteStep(
        ProcessRouteOptions options,
        string stepName)
        => $"{options.ProcessesRoutePrefix}/steps/{stepName}";
}
