using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.AllowedStep,Description = "Runtime allowed step", Version ="1.0")]
[DependsOnStep(typeof(RuntimeInvalidRequiredRootStep))]
public sealed record RuntimeAllowedStep;
