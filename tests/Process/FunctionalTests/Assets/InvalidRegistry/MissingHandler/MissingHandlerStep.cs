using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.InvalidRegistry.MissingHandler;

[ProcessStep("MissingHandlerStep", "MissingHandlerStep", "1.0")]
public sealed record MissingHandlerStep;
