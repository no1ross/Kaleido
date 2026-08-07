using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.InvalidRequiredRoot, Description = "Runtime invalid required root step", Version = "1.0")]
public sealed record RuntimeInvalidRequiredRootStep;
