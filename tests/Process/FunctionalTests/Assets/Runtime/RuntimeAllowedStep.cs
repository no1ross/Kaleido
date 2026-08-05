using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.AllowedStep, "Runtime allowed step", "1.0")]
[DependsOnStep(typeof(RuntimeInvalidRequiredRootStep))]
public sealed record RuntimeAllowedStep;
