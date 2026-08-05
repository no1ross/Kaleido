using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.RequiredRoot, "Runtime required root step", "1.0")]
public sealed record RuntimeRequiredRootStep;
