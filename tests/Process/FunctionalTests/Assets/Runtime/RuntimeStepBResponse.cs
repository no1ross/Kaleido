namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed record RuntimeStepBResponse
{
    public string Value { get; init; } = RuntimeStepNames.StepB;
}
