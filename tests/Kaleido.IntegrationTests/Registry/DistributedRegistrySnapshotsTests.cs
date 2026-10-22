using System.Net;
using Kaleido.Http;
using Kaleido.IntegrationTests.Guide;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kaleido.IntegrationTests.Registry;

/// <summary>
/// AddDistributedRegistrySnapshots() on real hosts (#275): replicas over one IDistributedCache share the
/// registry snapshot, and an unreachable cache never fails a registry read.
/// </summary>
public sealed class DistributedRegistrySnapshotsTests
{
    private static Task<GuideHost> StartReplicaAsync(IDistributedCache cache) =>
        GuideHost.StartAsync(
            configure: kaleido => kaleido.AddDistributedRegistrySnapshots(),
            services: s => s.AddSingleton(cache));

    [Fact]
    public async Task Replicas_ServeTheSnapshotOneOfThemBuilt()
    {
        var shared = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await using var replicaA = await StartReplicaAsync(shared);
        await using var replicaB = await StartReplicaAsync(shared);

        var (statusA, bodyA) = await replicaA.SendAsync(HttpMethod.Get, "/orders/registry");
        var (statusB, bodyB) = await replicaB.SendAsync(HttpMethod.Get, "/orders/registry");

        Assert.Equal(HttpStatusCode.OK, statusA);
        Assert.Equal(HttpStatusCode.OK, statusB);
        // A rebuild stamps its own build time: the same GeneratedAt means replica B did not build.
        Assert.Equal(bodyA.GetProperty("generatedAt").GetString(), bodyB.GetProperty("generatedAt").GetString());
    }

    [Fact]
    public async Task AnUnreachableCache_NeverFailsARegistryRead()
    {
        var unreachable = new Mock<IDistributedCache>();
        unreachable
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("The cache is unreachable."));
        unreachable
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("The cache is unreachable."));
        await using var replica = await StartReplicaAsync(unreachable.Object);

        var (status, body) = await replica.SendAsync(HttpMethod.Get, "/orders/registry");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEmpty(body.GetProperty("queryables").EnumerateArray());
    }
}
