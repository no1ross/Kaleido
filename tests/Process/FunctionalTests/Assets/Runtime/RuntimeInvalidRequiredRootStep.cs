using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.InvalidRequiredRoot, "Runtime invalid required root step", "1.0")]
public sealed record RuntimeInvalidRequiredRootStep;
