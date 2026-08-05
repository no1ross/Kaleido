namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed record RuntimeMergeStepResponse
{
    public string Value { get; init; } = RuntimeStepNames.Merge;
}
