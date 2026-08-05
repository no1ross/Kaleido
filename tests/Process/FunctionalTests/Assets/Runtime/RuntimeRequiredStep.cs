using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.RequiredStep, "Runtime required step", "1.0")]
[DependsOnStep(typeof(RuntimeRequiredRootStep))]
public sealed record RuntimeRequiredStep;
