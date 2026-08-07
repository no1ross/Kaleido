using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

[ProcessStep(Name = RuntimeStepNames.RequiredRoot, Description = "Runtime required root step", Version = "1.0")]
public sealed record RuntimeRequiredRootStep;
