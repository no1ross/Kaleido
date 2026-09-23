namespace Kaleido.Http.Process;

#pragma warning disable KAL0001 // Pure URL/name factory — no state, intentional static
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
        => $"steps/{stepName}/metadata";

    public static string ExecuteStep(
        string stepName)
        => $"steps/{stepName}";
}