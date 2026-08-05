using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.Merge, "Runtime merge step", "1.0")]
[DependsOnStep(typeof(RuntimeStepA))]
[DependsOnStep(typeof(RuntimeStepB))]
public sealed record RuntimeMergeStep;
