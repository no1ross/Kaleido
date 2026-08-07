using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.RequiredStep, Description = "Runtime required step", Version = "1.0")]
[DependsOnStep(typeof(RuntimeRequiredRootStep))]
public sealed record RuntimeRequiredStep;
