using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

[ProcessStep(Name = "RegistryChildStepB", Description = "RegistryChildStepB", Version = "1.0")]
[DependsOnStep(typeof(RegistryRootStep))]
public sealed record RegistryChildStepB;
