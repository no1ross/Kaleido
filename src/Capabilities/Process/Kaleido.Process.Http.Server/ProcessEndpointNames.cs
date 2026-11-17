namespace Kaleido.Process.Http.Server;

internal static class ProcessEndpointNames
{
    public const string ExecuteEndpointName = "KaleidoProcessExecute";

    public const string ProcessEndpointName = "KaleidoProcessState";

    public static string StepExecutionEndpointName(string stepName) => $"KaleidoProcessStepExecute_{stepName}";
}
