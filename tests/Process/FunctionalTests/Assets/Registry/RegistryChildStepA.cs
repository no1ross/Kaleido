using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

[ProcessStep(Name = "RegistryChildStepA", Description = "RegistryChildStepA", Version = "1.0")]
[DependsOnStep(typeof(RegistryRootStep))]
public sealed record RegistryChildStepA;
