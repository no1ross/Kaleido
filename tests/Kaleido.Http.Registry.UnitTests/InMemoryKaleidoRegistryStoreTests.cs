using Microsoft.Extensions.Logging;
using static Kaleido.Http.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Http.Registry.UnitTests;

// The store contract (IKaleidoRegistryStoreTests), on memory: one store per test is its database.
public sealed class InMemoryKaleidoRegistryStoreTests
    : IKaleidoRegistryStoreTests
{
    private readonly InMemoryKaleidoRegistryStore store = CreateSut();

    private static InMemoryKaleidoRegistryStore CreateSut(ILogger<InMemoryKaleidoRegistryStore>? logger = null) =>
        new(logger ?? Mock.Of<ILogger<InMemoryKaleidoRegistryStore>>());

    protected override IKaleidoRegistryStore CreateStore() => store;

    // A read is a copy: later writes do not change it.
    [Fact]
    public async Task Read_ReturnsACopy()
    {
        var read = await store.ReadAsync(Cancel);
        var (registration, capabilities) = Registration("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));

        await store.AddRegistrationAsync(registration, capabilities, Cancel);
        await store.SaveInstanceAsync(Instance(registration, Start), Cancel);
        await store.GetOrSetCurrentVersionAsync("radiology", "1", Cancel);

        Assert.Empty(read.Registrations);
        Assert.Empty(read.Instances);
        Assert.Empty(read.CurrentVersions);
    }

    [Fact]
    public void Created_WarnsThatNothingSurvivesARestart()
    {
        var logger = new Mock<ILogger<InMemoryKaleidoRegistryStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _ = CreateSut(logger.Object);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log)).Arguments[0]);
    }
}
