using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.StepA, "Runtime step A", "1.0")]
[DependsOnStep(typeof(RuntimeRootStep))]
public sealed record RuntimeStepA;
