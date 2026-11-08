using Kaleido.Platform.Registry;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Registry.UnitTests;

public sealed class RegistryHybridCacheTests : SutFixture
{
    private readonly Mock<HybridCache> cache = new();
    private readonly KaleidoRegistryCacheOptions options = new()
    {
        KeyPrefix = "tenant:registry",
        ViewTtl = TimeSpan.FromSeconds(4),
        ViewLocalTtl = TimeSpan.FromSeconds(2),
        CapabilitiesTtl = TimeSpan.FromDays(20),
        CapabilitiesLocalTtl = TimeSpan.FromDays(5),
    };

    private RegistryHybridCache CreateSut() => new(cache.Object, options, NullLogger<RegistryHybridCache>.Instance);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AView_UsesTheConfiguredKeyAndBothLifetimes()
    {
        var expected = new RegistryView(new(), new(), new(), new());
        cache.Setup(x => x.GetOrCreateAsync<Func<CancellationToken, Task<RegistryView>>, RegistryView>(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<RegistryView>>>(), It.IsAny<Func<Func<CancellationToken, Task<RegistryView>>, CancellationToken, ValueTask<RegistryView>>>(), It.IsAny<HybridCacheEntryOptions>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<RegistryView>(expected));

        var result = await CreateSut().GetViewAsync(_ => Task.FromResult(expected), Cancel);

        Assert.Same(expected, result);
        cache.Verify(x => x.GetOrCreateAsync<Func<CancellationToken, Task<RegistryView>>, RegistryView>("tenant:registry:view", It.IsAny<Func<CancellationToken, Task<RegistryView>>>(), It.IsAny<Func<Func<CancellationToken, Task<RegistryView>>, CancellationToken, ValueTask<RegistryView>>>(),
            It.Is<HybridCacheEntryOptions>(entry => entry.Expiration == options.ViewTtl && entry.LocalCacheExpiration == options.ViewLocalTtl),
            It.IsAny<IEnumerable<string>?>(), Cancel), Times.Once);
    }

    [Fact]
    public async Task Capabilities_UseTheRevisionKeyAndConfiguredLifetimes()
    {
        var expected = new KaleidoRegistry();
        cache.Setup(x => x.GetOrCreateAsync<Func<CancellationToken, Task<KaleidoRegistry>>, KaleidoRegistry>(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), It.IsAny<Func<Func<CancellationToken, Task<KaleidoRegistry>>, CancellationToken, ValueTask<KaleidoRegistry>>>(), It.IsAny<HybridCacheEntryOptions>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<KaleidoRegistry>(expected));

        var result = await CreateSut().GetCapabilitiesAsync("abc123", _ => Task.FromResult(expected), Cancel);

        Assert.Same(expected, result);
        cache.Verify(x => x.GetOrCreateAsync<Func<CancellationToken, Task<KaleidoRegistry>>, KaleidoRegistry>("tenant:registry:capabilities:abc123", It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), It.IsAny<Func<Func<CancellationToken, Task<KaleidoRegistry>>, CancellationToken, ValueTask<KaleidoRegistry>>>(),
            It.Is<HybridCacheEntryOptions>(entry => entry.Expiration == options.CapabilitiesTtl && entry.LocalCacheExpiration == options.CapabilitiesLocalTtl),
            It.IsAny<IEnumerable<string>?>(), Cancel), Times.Once);
    }

    [Fact]
    public async Task ADistributedCacheFailure_DoesNotPreventViewInvalidation()
    {
        cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(Task.FromException(new IOException("cache unavailable"))));

        await CreateSut().InvalidateViewAsync(Cancel);

        cache.Verify(x => x.RemoveAsync("tenant:registry:view", Cancel), Times.Once);
    }

    [Fact]
    public async Task ACancelledInvalidation_PropagatesCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        await cancellation.CancelAsync();
        cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(Task.FromException(new OperationCanceledException(cancellation.Token))));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().InvalidateViewAsync(cancellation.Token));
    }

    [Fact]
    public async Task Refresh_InvalidatesOnlyTheMergedView()
    {
        cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        await CreateSut().InvalidateViewAsync(Cancel);

        cache.Verify(x => x.RemoveAsync("tenant:registry:view", Cancel), Times.Once);
    }
}
