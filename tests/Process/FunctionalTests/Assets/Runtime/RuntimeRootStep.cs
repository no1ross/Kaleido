using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(RuntimeStepNames.Root, "Runtime root step", "1.0")]
public sealed record RuntimeRootStep;
