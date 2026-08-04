using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

[ProcessStep("RegistryChildStepA", "RegistryChildStepA", "1.0")]
[DependsOnStep(typeof(RegistryRootStep))]
public sealed record RegistryChildStepA;
