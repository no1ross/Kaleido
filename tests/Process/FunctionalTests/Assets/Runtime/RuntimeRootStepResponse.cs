namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed record RuntimeRootStepResponse
{
    public string Value { get; init; } = RuntimeStepNames.Root;
}
