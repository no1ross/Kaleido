using Xunit;

namespace Kaleido.Process.FunctionalTests.Fixtures;

[CollectionDefinition(nameof(RuntimeTestCollection))]
public sealed class RuntimeTestCollection :
    ICollectionFixture<RuntimeTestFixture>
{
}
