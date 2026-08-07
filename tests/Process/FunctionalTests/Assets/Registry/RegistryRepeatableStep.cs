using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

[ProcessStep(Name = "registry-repeatable-step", Description = "registry-repeatable-step description", Version = "1.0")]
[Repeatable]
public sealed class RegistryRepeatableStep
{
}