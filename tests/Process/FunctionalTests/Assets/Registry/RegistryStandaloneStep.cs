using Kaleido.Process.Attributes;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

/// <summary>
/// A valid step intentionally disconnected from the main registry test graph.
/// This verifies that standalone/root-only process steps are valid registrations.
/// </summary>
[ProcessStep(Name = "RegistryStandaloneStep", Description = "RegistryStandaloneStep", Version = "1.0")]
public sealed record RegistryStandaloneStep;
