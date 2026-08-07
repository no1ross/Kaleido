using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.StepA, Description = "Runtime step A", Version = "1.0")]
[DependsOnStep(typeof(RuntimeRootStep))]
public sealed record RuntimeStepA;
