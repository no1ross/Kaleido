using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Process.AspNetCore.FunctionalTests;

[CollectionDefinition(nameof(ProcessAspNetCoreCollection))]
public sealed class ProcessAspNetCoreCollection
    : ICollectionFixture<ProcessAspNetCoreFixture>;
