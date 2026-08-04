using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

[ProcessStep("RegistryMergeStep", "RegistryMergeStep", "1.0")]
[DependsOnStep(typeof(RegistryChildStepA))]
[DependsOnStep(typeof(RegistryChildStepB))]
public sealed record RegistryMergeStep;
