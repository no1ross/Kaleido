using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.StepB, "Runtime step B", "1.0")]
[DependsOnStep(typeof(RuntimeRootStep))]
public sealed record RuntimeStepB;
