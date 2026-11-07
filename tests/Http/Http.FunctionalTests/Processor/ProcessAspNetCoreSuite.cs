using Kaleido.Http.FunctionalTests.Processor.Fixtures;

namespace Kaleido.Http.FunctionalTests.Processor;

[CollectionDefinition(nameof(ProcessorAspNetCoreSuite))]
public sealed class ProcessorAspNetCoreSuite
    : ICollectionFixture<ProcessorAspNetCoreFixture>;
