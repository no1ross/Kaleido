using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.Merge, Description = "Runtime merge step", Version = "1.0")]
[DependsOnStep(typeof(RuntimeStepA))]
[DependsOnStep(typeof(RuntimeStepB))]
public sealed record RuntimeMergeStep;
