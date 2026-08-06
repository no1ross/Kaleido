using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

[ProcessStep(
    "registry-repeatable-step",
    "registry-repeatable-step description",
    "1.0")]
[Repeatable]
public sealed class RegistryRepeatableStep
{
}