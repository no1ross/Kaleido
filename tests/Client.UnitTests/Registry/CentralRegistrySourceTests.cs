using Kaleido.Platform.Processor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Client.Registry.UnitTests;

// #553: a participant's registry is the control registry's, kept in memory: two documents, each checked on
// its own interval with its ETag, composed into the one registry every reader gets.
public sealed class CentralRegistrySourceTests
    : Kaleido.UnitTests.SutFixture, IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    // The pace the registry hands out with its availability.
    private static readonly TimeSpan AvailabilityEvery = TimeSpan.FromSeconds(10);

    private static readonly KaleidoRegistry Capabilities = new()
    {
        Processes = [new ProcessorRegistryResponse { Name = "code-sets", ServiceName = "code-sets", ServiceVersion = "3" }],
        Revision = "caps-1",
    };

    private static readonly KaleidoRegistryAvailability Available = Availability(RegistryCapabilityStatus.Available, "avail-1");

    private readonly FakeTimeProvider time = new(Start);
    private readonly Mock<IKaleidoRegistryClient> client = new();
    private readonly List<(string Document, string? ETag)> requests = [];
    private readonly Dictionary<string, Func<string?, object>> answers = new(StringComparer.Ordinal);
    private readonly List<CentralRegistrySource> created = [];

    public CentralRegistrySourceTests()
    {
        answers["capabilities"] = _ => Ok(Capabilities, "caps-1");
        answers["availability"] = _ => Ok(Available, "avail-1");
        client.Setup(x => x.GetCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((string? etag, CancellationToken _) => AnswerAsync<KaleidoRegistry>("capabilities", etag));
        client.Setup(x => x.GetAvailabilityAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((string? etag, CancellationToken _) => AnswerAsync<KaleidoRegistryAvailability>("availability", etag));
    }

    private Task<KaleidoRegistryDocument<T>> AnswerAsync<T>(string document, string? etag) where T : class
    {
        requests.Add((document, etag));
        var result = answers[document](etag);
        return result is Exception exception
            ? Task.FromException<KaleidoRegistryDocument<T>>(exception)
            : Task.FromResult((KaleidoRegistryDocument<T>)result);
    }

    private CentralRegistrySource CreateSut()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IKaleidoRegistryClient))).Returns(client.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);
        var sut = new CentralRegistrySource(factory.Object, time, NullLogger<CentralRegistrySource>.Instance);
        created.Add(sut);
        return sut;
    }

    public void Dispose()
    {
        foreach (var sut in created)
        {
            sut.Dispose();
        }

    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static KaleidoRegistryAvailability Availability(RegistryCapabilityStatus status, string revision) =>
        new()
        {
            Services = [new RegistryServiceAvailability { ServiceName = "code-sets", ServiceVersion = "3", IsCurrent = true, Status = status }],
            AvailabilityInterval = AvailabilityEvery,
            CapabilitiesRevision = "caps-1",
            Revision = revision,
            GeneratedAt = Start,
        };

    private static KaleidoRegistryDocument<T> Ok<T>(T value, string etag) where T : class =>
        new(value, $"\"{etag}\"", NotModified: false);

    private static KaleidoRegistryDocument<T> Unchanged<T>() where T : class =>
        new(null, null, NotModified: true);

    private static KaleidoRegistryClientException Failed(string? status) =>
        new(RegistryClientErrorCodes.RequestFailed, "The registry could not be read.", status);

    private int Reads(string document) => requests.Count(x => x.Document == document);

    [Fact]
    public async Task BeforeTheFirstRead_TheRegistryIsEmpty_AndPartial()
    {
        var sut = CreateSut();

        var registry = await sut.GetAsync(Cancel);

        Assert.Empty(registry.Processes);
        Assert.Equal(CentralRegistrySource.ErrorSource, Assert.Single(registry.Errors).Source);
        Assert.Null(registry.Revision);
        Assert.False(sut.HasRead);
    }

    // Each capability carries its version's overall status; the revision follows both documents.
    [Fact]
    public async Task AfterASync_TheRegistry_IsTheCapabilitiesWithTheirStatus()
    {
        var sut = CreateSut();

        await sut.SyncAsync(force: true, Cancel);

        var registry = await sut.GetAsync(Cancel);
        Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single(registry.Processes).Status);
        Assert.Empty(registry.Errors);
        Assert.Matches("^[0-9a-f]{64}$", registry.Revision!);
        Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single((await sut.GetAvailabilityAsync(Cancel)).Services).Status);
        Assert.True(sut.HasRead);
        client.Verify(x => x.GetCapabilitiesAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The capabilities held are the ones the availability names: never read again, however long.
    [Fact]
    public async Task CurrentCapabilities_AreNotReadAgain()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);

        for (var i = 0; i < 10; i++)
        {
            time.Advance(AvailabilityEvery);
            await sut.SyncAsync(force: false, Cancel);
        }

        Assert.Equal((1, 11), (Reads("capabilities"), Reads("availability")));
    }

    // A deployment: the availability names other capabilities, which are read in the same check.
    [Fact]
    public async Task NewCapabilities_AreReadInTheSameCheck()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        var deployed = Capabilities with { Processes = [.. Capabilities.Processes, new ProcessorRegistryResponse { Name = "member", ServiceName = "member", ServiceVersion = "1" }], Revision = "caps-2" };
        answers["capabilities"] = _ => Ok(deployed, "caps-2");
        answers["availability"] = _ => Ok(Available with { CapabilitiesRevision = "caps-2", Revision = "avail-2" }, "avail-2");

        time.Advance(AvailabilityEvery);
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(2, Reads("capabilities"));
        Assert.Equal(["code-sets", "member"], (await sut.GetAsync(Cancel)).Processes.Select(x => x.ServiceName).Order());
    }

    // An availability naming no capabilities revision: they are checked each time, with their ETag.
    [Fact]
    public async Task WithoutACapabilitiesRevision_TheCapabilitiesAreCheckedEachTime()
    {
        answers["availability"] = _ => Ok(Available with { CapabilitiesRevision = null }, "avail-1");
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        answers["capabilities"] = _ => Unchanged<KaleidoRegistry>();

        time.Advance(AvailabilityEvery);
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(2, Reads("capabilities"));
        Assert.Equal("\"caps-1\"", requests.Last(x => x.Document == "capabilities").ETag);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    public async Task TheAvailability_IsCheckedOnItsInterval(long ticksFromTheInterval, int reads)
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);

        time.Advance(AvailabilityEvery + TimeSpan.FromTicks(ticksFromTheInterval));
        answers["availability"] = _ => Unchanged<KaleidoRegistryAvailability>();
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(reads, Reads("availability"));
    }

    // A check sends the held ETag; 304 keeps the registry, and its revision, as they were.
    [Fact]
    public async Task ANotModifiedAnswer_KeepsTheRegistry()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        var first = await sut.GetAsync(Cancel);

        answers["capabilities"] = _ => Unchanged<KaleidoRegistry>();
        answers["availability"] = _ => Unchanged<KaleidoRegistryAvailability>();
        await sut.SyncAsync(force: true, Cancel);

        Assert.Equal(first.Revision, (await sut.GetAsync(Cancel)).Revision);
        Assert.Equal("\"caps-1\"", requests.Last(x => x.Document == "capabilities").ETag);
    }

    // A status flip is a new revision: the host rebuilds what it serves.
    [Fact]
    public async Task AStatusFlip_IsANewRegistry()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        var first = await sut.GetAsync(Cancel);

        answers["availability"] = _ => Ok(Availability(RegistryCapabilityStatus.Unavailable, "avail-2"), "avail-2");
        time.Advance(AvailabilityEvery);
        await sut.SyncAsync(force: false, Cancel);

        var flipped = await sut.GetAsync(Cancel);
        Assert.Equal(RegistryCapabilityStatus.Unavailable, Assert.Single(flipped.Processes).Status);
        Assert.NotEqual(first.Revision, flipped.Revision);
    }

    // A failed read keeps the last copy, partial and without a revision; the next good read recovers it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AFailedRead_KeepsTheLastCopy_PartialUntilItRecovers(bool unreachable)
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        answers["availability"] = _ => unreachable ? Failed(null) : Failed("503");

        await sut.SyncAsync(force: true, Cancel);
        var failed = await sut.GetAsync(Cancel);
        answers["availability"] = _ => Ok(Available, "avail-1");
        await sut.SyncAsync(force: true, Cancel);
        var recovered = await sut.GetAsync(Cancel);

        Assert.Single(failed.Processes);
        Assert.Equal(CentralRegistrySource.ErrorSource, Assert.Single(failed.Errors).Source);
        Assert.Null(failed.Revision);
        Assert.Empty(recovered.Errors);
        Assert.NotNull(recovered.Revision);
    }

    [Fact]
    public async Task Refresh_ReadsBothNow()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);

        await sut.RefreshAsync(Cancel);

        Assert.Equal((2, 2), (Reads("capabilities"), Reads("availability")));
    }

    // A read that never succeeded says why.
    [Fact]
    public async Task WhenTheFirstReadFails_TheRegistryStaysEmpty_WithTheReason()
    {
        answers["capabilities"] = _ => Failed("403");
        var sut = CreateSut();

        await sut.SyncAsync(force: true, Cancel);

        var registry = await sut.GetAsync(Cancel);
        Assert.Empty(registry.Processes);
        Assert.Contains("403", Assert.Single(registry.Errors).Reason, StringComparison.Ordinal);
        Assert.False(sut.HasRead);
    }

    // The registry sets the pace: the next check is when its interval is due.
    [Fact]
    public async Task TheNextCheck_FollowsTheRegistrysInterval()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);

        Assert.Equal(AvailabilityEvery, sut.NextCheck);
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(AvailabilityEvery - TimeSpan.FromSeconds(4), sut.NextCheck);

        answers["availability"] = _ => Ok(Available with { AvailabilityInterval = TimeSpan.FromMinutes(5), Revision = "avail-slow" }, "avail-slow");
        await sut.SyncAsync(force: true, Cancel);

        Assert.Equal(TimeSpan.FromMinutes(5), sut.NextCheck);
    }

    // Nothing to follow yet, or a read that failed: the retry, 1 s doubling - never an interval of the host's.
    [Fact]
    public async Task WithoutTheRegistrysPace_TheNextCheck_IsTheRetry()
    {
        answers["availability"] = _ => Failed("503");
        var sut = CreateSut();

        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        await sut.SyncAsync(force: true, Cancel);
        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        await sut.SyncAsync(force: false, Cancel);
        Assert.Equal(RegistryRetry.After(1), sut.NextCheck);

        answers["availability"] = _ => Ok(Available, "avail-1");
        await sut.SyncAsync(force: false, Cancel);
        Assert.Equal(AvailabilityEvery, sut.NextCheck);
    }

    // A registry naming no interval is checked at the retry's pace, never at one the host invents.
    [Fact]
    public async Task ARegistryNamingNoInterval_IsCheckedAtTheRetrysPace()
    {
        answers["availability"] = _ => Ok(Available with { AvailabilityInterval = TimeSpan.Zero }, "avail-1");
        var sut = CreateSut();

        await sut.SyncAsync(force: true, Cancel);
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        Assert.Equal(2, Reads("availability"));
    }

    // A registry that stops answering after a good read: the host backs off, never checking at the
    // registry's interval while it cannot be read.
    [Fact]
    public async Task AfterAGoodRead_AFailedRead_BacksOff()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        answers["availability"] = _ => Failed("503");

        await sut.SyncAsync(force: true, Cancel);
        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        await sut.SyncAsync(force: true, Cancel);
        Assert.Equal(RegistryRetry.After(1), sut.NextCheck);
    }
}
