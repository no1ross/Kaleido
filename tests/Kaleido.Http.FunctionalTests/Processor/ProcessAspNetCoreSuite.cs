using Kaleido.Processor.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Processor.AspNetCore.FunctionalTests;

[CollectionDefinition(nameof(ProcessorAspNetCoreSuite))]
public sealed class ProcessorAspNetCoreSuite
    : ICollectionFixture<ProcessorAspNetCoreFixture>;
