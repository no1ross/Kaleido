using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.InvalidRegistry.MissingHandler;

[ProcessStep(Name ="MissingHandlerStep", Description = "MissingHandlerStep", Version = "1.0")]
public sealed record MissingHandlerStep;
