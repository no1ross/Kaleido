using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Process.AspNetCore.FunctionalTests;

[CollectionDefinition(nameof(ProcessAspNetCoreSuite))]
public sealed class ProcessAspNetCoreSuite
    : ICollectionFixture<ProcessAspNetCoreFixture>;
