namespace Kaleido.Process.Http.Server;

internal static class ProcessRoutePaths
{
    public const string Process = "{processId}";

    public const string Execute = "execute";

    public static string ExecuteStep(string stepName) => $"steps/{stepName}";
}
