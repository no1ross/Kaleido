using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.StepB, Description = "Runtime step B", Version = "1.0")]
[DependsOnStep(typeof(RuntimeRootStep))]
public sealed record RuntimeStepB;
