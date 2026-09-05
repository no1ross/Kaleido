namespace Kaleido.Process.AspNetCore.Contracts;

internal static class ProcessRoutePaths
{
    public const string Process =
        "{processId}";

    public const string StepCatalog =
        "steps";

    public const string StepRegistry =
        "registry";

    public const string Execute =
        "execute";

    public static string StepMetadata(
        string stepName)
        => $"steps/{stepName}";

    public static string ExecuteStep(
        string stepName)
        => $"steps/{stepName}";
}