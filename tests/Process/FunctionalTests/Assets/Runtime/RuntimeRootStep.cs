using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.Root, Description = "Runtime root step", Version = "1.0")]
public sealed record RuntimeRootStep;
