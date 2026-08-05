namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed record RuntimeStepAResponse
{
    public string Value { get; init; } = RuntimeStepNames.StepA;
}
