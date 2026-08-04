using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

[ProcessStep("RegistryChildStepB", "RegistryChildStepB", "1.0")]
[DependsOnStep(typeof(RegistryRootStep))]
public sealed record RegistryChildStepB;
