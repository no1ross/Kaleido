using System.Net.Http.Headers;
using Kaleido.Http.Processor;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.Client.UnitTests;

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
    private readonly Mock<HttpMessageHandler> handler = new(MockBehavior.Strict);
    private readonly List<HttpRequestMessage> requests = [];
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> answers = new(StringComparer.Ordinal);
    private readonly List<CentralRegistrySource> created = [];

    public CentralRegistrySourceTests()
    {
        answers["/registry/capabilities"] = _ => Ok(Capabilities, "caps-1");
        answers["/registry/availability"] = _ => Ok(Available, "avail-1");
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                requests.Add(request);
                return answers[request.RequestUri!.AbsolutePath](request);
            });
    }

    private CentralRegistrySource CreateSut()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(CentralRegistrySource.ClientName))
            .Returns(() => new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://registry.internal/") });
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

    private static HttpResponseMessage Ok<T>(T value, string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: KaleidoJsonOptions.Options) };
        response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
        return response;
    }

    private int Reads(string path) => requests.Count(x => x.RequestUri!.AbsolutePath == path);

    [Fact]
    public async Task BeforeTheFirstRead_TheRegistryIsEmpty_AndPartial()
    {
        var sut = CreateSut();

        var registry = await sut.GetAsync(null, Cancel);

        Assert.Empty(registry.Processes);
        Assert.Equal(CentralRegistrySource.ErrorSource, Assert.Single(registry.Errors).Source);
        Assert.Null(registry.Revision);
        Assert.False(sut.HasRead);
        Assert.True(sut.IsFullRegistry);
    }

    // Each capability carries its version's overall status; the revision follows both documents.
    [Fact]
    public async Task AfterASync_TheRegistry_IsTheCapabilitiesWithTheirStatus()
    {
        var sut = CreateSut();

        await sut.SyncAsync(force: true, Cancel);

        var registry = await sut.GetAsync(null, Cancel);
        Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single(registry.Processes).Status);
        Assert.Empty(registry.Errors);
        Assert.Matches("^[0-9a-f]{64}$", registry.Revision!);
        Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single((await sut.GetAvailabilityAsync(Cancel)).Services).Status);
        Assert.True(sut.HasRead);
        Assert.All(requests, x => Assert.Equal(KaleidoCallPurpose.ServiceOwn, Assert.IsType<KaleidoCallContext>(x.Options.TryGetValue(KaleidoCallContext.Key, out var context) ? context : null).Purpose));
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

        Assert.Equal((1, 11), (Reads("/registry/capabilities"), Reads("/registry/availability")));
    }

    // A deployment: the availability names other capabilities, which are read in the same check.
    [Fact]
    public async Task NewCapabilities_AreReadInTheSameCheck()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        var deployed = Capabilities with { Processes = [.. Capabilities.Processes, new ProcessorRegistryResponse { Name = "member", ServiceName = "member", ServiceVersion = "1" }], Revision = "caps-2" };
        answers["/registry/capabilities"] = _ => Ok(deployed, "caps-2");
        answers["/registry/availability"] = _ => Ok(Available with { CapabilitiesRevision = "caps-2", Revision = "avail-2" }, "avail-2");

        time.Advance(AvailabilityEvery);
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(2, Reads("/registry/capabilities"));
        Assert.Equal(["code-sets", "member"], (await sut.GetAsync(null, Cancel)).Processes.Select(x => x.ServiceName).Order());
    }

    // An availability naming no capabilities revision: they are checked each time, with their ETag.
    [Fact]
    public async Task WithoutACapabilitiesRevision_TheCapabilitiesAreCheckedEachTime()
    {
        answers["/registry/availability"] = _ => Ok(Available with { CapabilitiesRevision = null }, "avail-1");
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        answers["/registry/capabilities"] = _ => new HttpResponseMessage(HttpStatusCode.NotModified);

        time.Advance(AvailabilityEvery);
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(2, Reads("/registry/capabilities"));
        Assert.Equal("\"caps-1\"", requests.Last(x => x.RequestUri!.AbsolutePath == "/registry/capabilities").Headers.IfNoneMatch.Single().ToString());
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    public async Task TheAvailability_IsCheckedOnItsInterval(long ticksFromTheInterval, int reads)
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);

        time.Advance(AvailabilityEvery + TimeSpan.FromTicks(ticksFromTheInterval));
        answers["/registry/availability"] = _ => new HttpResponseMessage(HttpStatusCode.NotModified);
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(reads, Reads("/registry/availability"));
    }

    // A check sends the held ETag; 304 keeps the registry, and its revision, as they were.
    [Fact]
    public async Task ANotModifiedAnswer_KeepsTheRegistry()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        var first = await sut.GetAsync(null, Cancel);

        answers["/registry/capabilities"] = _ => new HttpResponseMessage(HttpStatusCode.NotModified);
        answers["/registry/availability"] = _ => new HttpResponseMessage(HttpStatusCode.NotModified);
        await sut.SyncAsync(force: true, Cancel);

        Assert.Equal(first.Revision, (await sut.GetAsync(null, Cancel)).Revision);
        Assert.Equal("\"caps-1\"", requests.Last(x => x.RequestUri!.AbsolutePath == "/registry/capabilities").Headers.IfNoneMatch.Single().ToString());
    }

    // A status flip is a new revision: the host rebuilds what it serves.
    [Fact]
    public async Task AStatusFlip_IsANewRegistry()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        var first = await sut.GetAsync(null, Cancel);

        answers["/registry/availability"] = _ => Ok(Availability(RegistryCapabilityStatus.Unavailable, "avail-2"), "avail-2");
        time.Advance(AvailabilityEvery);
        await sut.SyncAsync(force: false, Cancel);

        var flipped = await sut.GetAsync(null, Cancel);
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
        answers["/registry/availability"] = _ => unreachable ? throw new HttpRequestException("down") : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        await sut.SyncAsync(force: true, Cancel);
        var failed = await sut.GetAsync(null, Cancel);
        answers["/registry/availability"] = _ => Ok(Available, "avail-1");
        await sut.SyncAsync(force: true, Cancel);
        var recovered = await sut.GetAsync(null, Cancel);

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

        Assert.Equal((2, 2), (Reads("/registry/capabilities"), Reads("/registry/availability")));
    }

    // A read that never succeeded says why.
    [Fact]
    public async Task WhenTheFirstReadFails_TheRegistryStaysEmpty_WithTheReason()
    {
        answers["/registry/capabilities"] = _ => new HttpResponseMessage(HttpStatusCode.Forbidden);
        var sut = CreateSut();

        await sut.SyncAsync(force: true, Cancel);

        var registry = await sut.GetAsync(null, Cancel);
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

        answers["/registry/availability"] = _ => Ok(Available with { AvailabilityInterval = TimeSpan.FromMinutes(5), Revision = "avail-slow" }, "avail-slow");
        await sut.SyncAsync(force: true, Cancel);

        Assert.Equal(TimeSpan.FromMinutes(5), sut.NextCheck);
    }

    // Nothing to follow yet, or a read that failed: the retry, 1 s doubling - never an interval of the host's.
    [Fact]
    public async Task WithoutTheRegistrysPace_TheNextCheck_IsTheRetry()
    {
        answers["/registry/availability"] = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var sut = CreateSut();

        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        await sut.SyncAsync(force: true, Cancel);
        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        await sut.SyncAsync(force: false, Cancel);
        Assert.Equal(RegistryRetry.After(1), sut.NextCheck);

        answers["/registry/availability"] = _ => Ok(Available, "avail-1");
        await sut.SyncAsync(force: false, Cancel);
        Assert.Equal(AvailabilityEvery, sut.NextCheck);
    }

    // A registry naming no interval is checked at the retry's pace, never at one the host invents.
    [Fact]
    public async Task ARegistryNamingNoInterval_IsCheckedAtTheRetrysPace()
    {
        answers["/registry/availability"] = _ => Ok(Available with { AvailabilityInterval = TimeSpan.Zero }, "avail-1");
        var sut = CreateSut();

        await sut.SyncAsync(force: true, Cancel);
        await sut.SyncAsync(force: false, Cancel);

        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        Assert.Equal(2, Reads("/registry/availability"));
    }

    // A registry that stops answering after a good read: the host backs off, never checking at the
    // registry's interval while it cannot be read.
    [Fact]
    public async Task AfterAGoodRead_AFailedRead_BacksOff()
    {
        var sut = CreateSut();
        await sut.SyncAsync(force: true, Cancel);
        answers["/registry/availability"] = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        await sut.SyncAsync(force: true, Cancel);
        Assert.Equal(RegistryRetry.First, sut.NextCheck);
        await sut.SyncAsync(force: true, Cancel);
        Assert.Equal(RegistryRetry.After(1), sut.NextCheck);
    }
}
