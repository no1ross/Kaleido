using System.Collections.Concurrent;
using Kaleido.Exceptions;
using Kaleido.Http.Authorization;
using Kaleido.Http.Queryable;
using Kaleido.Http.Registry;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class KaleidoRegistryServiceTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly AuthorizationMetadata Radiology = new(null, ["radiology"]);

    private readonly ConcurrentDictionary<string, AggregatedRegistryResponse> stored = new(StringComparer.OrdinalIgnoreCase);
    private readonly Mock<IKaleidoDownstreamRegistries> downstream = new();
    private readonly Mock<IKaleidoAuthorizer> authorizer = new();
    private readonly Mock<IProcessorResponseFactory> responses = new();
    private readonly List<ProcessorRegistryItem> localProcessors = [new()];
    private readonly KaleidoServiceOptions serviceOptions = new() { ServiceName = "router" };

    public KaleidoRegistryServiceTests()
    {
        responses.Setup(x => x.CreateRegistryResponse(It.IsAny<ProcessorRegistryItem>(), It.IsAny<KaleidoServiceOptions>()))
            .Returns((ProcessorRegistryItem _, KaleidoServiceOptions options) => new ProcessorRegistryResponse
            {
                Name = options.ServiceName,
                ServiceName = options.ServiceName,
                Steps = [new ProcessStepResponse { Name = "CaptureMemberStep" }]
            });

        downstream.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownstreamRegistries(
                [new ProcessorRegistryResponse { Name = "radiology", ServiceName = "radiology", Steps = [new ProcessStepResponse { Name = "CaptureScanStep", Authorization = Radiology }] }],
                [new QueryableSourceResponse { Name = "MemberSource", ServiceName = "member", QueryUrl = "/member/q" }],
                []));

        authorizer.Setup(x => x.IsEnforced).Returns(false);
    }

    private (KaleidoRegistryService Sut, RegistrySnapshot Snapshot) CreateSut(bool aggregate = true, TimeSpan? cooldown = null)
    {
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) => new ValueTask<AggregatedRegistryResponse?>(stored.TryGetValue(k, out var v) ? v : null));
        store.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AggregatedRegistryResponse>(), It.IsAny<CancellationToken>()))
            .Returns((string k, AggregatedRegistryResponse v, CancellationToken _) =>
            {
                stored[k] = v;
                return ValueTask.CompletedTask;
            });

        var snapshot = new RegistrySnapshot(store.Object, serviceOptions);
        snapshot.Configure(new KaleidoHttpMapOptions
        {
            AggregateRegistry = aggregate,
            RegistryRefreshCooldown = cooldown ?? TimeSpan.FromSeconds(30)
        });

        var processors = new Mock<IProcessorRegistry>();
        processors.Setup(x => x.Registrations).Returns(localProcessors);

        var queryables = new Mock<IQueryableRegistry>();
        queryables.Setup(x => x.Registrations).Returns([]);

        var local = new LocalRegistrations([processors.Object], [queryables.Object], responses.Object, serviceOptions);

        return (
            new KaleidoRegistryService(snapshot, local, downstream.Object, authorizer.Object, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), NullLogger<KaleidoRegistryService>.Instance),
            snapshot);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetForCaller_MergesLocalAndDownstream_WhenAggregating()
    {
        var registry = await CreateSut().Sut.GetForCallerAsync(cancellationToken: Cancel);

        Assert.Equal(["radiology", "router"], registry.Processes.Select(x => x.Name));
        Assert.Equal(["MemberSource"], registry.Queryables.Select(x => x.Name));
        Assert.False(string.IsNullOrWhiteSpace(registry.Revision));
    }

    // The revision is the ETag a client checks with: a rebuild (a new GeneratedAt) with the same
    // content must keep it, or every check would download the registry again.
    [Fact]
    public async Task GetForCaller_WhenRebuiltWithTheSameContent_KeepsTheRevision()
    {
        var sut = CreateSut(cooldown: TimeSpan.Zero).Sut;

        var first = await sut.GetForCallerAsync(cancellationToken: Cancel);
        await Task.Delay(TimeSpan.FromMilliseconds(20), Cancel);
        var rebuilt = await sut.GetForCallerAsync(refresh: true, cancellationToken: Cancel);

        Assert.NotEqual(first.GeneratedAt, rebuilt.GeneratedAt);
        Assert.Equal(first.Revision, rebuilt.Revision);
    }

    [Fact]
    public async Task GetForCaller_WithoutAggregation_ServesOnlyLocalRegistrations()
    {
        var registry = await CreateSut(aggregate: false).Sut.GetForCallerAsync(cancellationToken: Cancel);

        Assert.Equal(["router"], registry.Processes.Select(x => x.Name));
        downstream.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetForCaller_ServesTheSharedSnapshot_UntilRefreshed()
    {
        var (sut, _) = CreateSut(cooldown: TimeSpan.Zero);

        await sut.GetForCallerAsync(cancellationToken: Cancel);
        await sut.GetForCallerAsync(cancellationToken: Cancel);
        downstream.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);

        await sut.GetForCallerAsync(refresh: true, cancellationToken: Cancel);

        downstream.Verify(x => x.InvalidateAsync(Cancel), Times.Once);
        downstream.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetForCaller_APartialAggregate_IsServedButNotKept()
    {
        downstream.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownstreamRegistries([], [], [new RegistryClientError { ClientName = "radiology", ClientType = "Registry", Reason = "down" }]));
        var (sut, _) = CreateSut();

        var first = await sut.GetForCallerAsync(cancellationToken: Cancel);
        await sut.GetForCallerAsync(cancellationToken: Cancel);

        Assert.True(first.IsPartial);
        Assert.Equal("radiology", Assert.Single(first.ClientErrors).ClientName);
        downstream.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetForCaller_WhenEnforced_FiltersForTheCaller_AndTheRevisionFollowsTheContent()
    {
        var (sut, _) = CreateSut();
        var unfiltered = await sut.GetForCallerAsync(cancellationToken: Cancel);

        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> rule, CancellationToken _) =>
                items.Where(x => rule(x) != Radiology).ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<QueryableViewResponse>>(), It.IsAny<Func<QueryableViewResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<QueryableViewResponse> items, Func<QueryableViewResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());

        var filtered = await sut.GetForCallerAsync(cancellationToken: Cancel);

        Assert.Equal(["router"], filtered.Processes.Select(x => x.Name));
        Assert.NotEqual(unfiltered.Revision, filtered.Revision);
        Assert.Equal(filtered.Revision, (await sut.GetForCallerAsync(cancellationToken: Cancel)).Revision);
    }

    [Fact]
    public async Task GetForCaller_ASourceTheCallerMayNotQueryDirectly_KeepsItsAllowedViews_WithoutAQueryUrl()
    {
        downstream.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownstreamRegistries(
                [],
                [new QueryableSourceResponse
                {
                    Name = "ChartSource",
                    ServiceName = "clinic",
                    QueryUrl = "/clinic/q",
                    Authorization = Radiology,
                    Views = [new QueryableViewResponse { Name = "ChartCard", QueryUrl = "/clinic/v" }]
                }],
                []));
        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthorizationMetadata? rule, CancellationToken _) => rule != Radiology);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<QueryableViewResponse>>(), It.IsAny<Func<QueryableViewResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<QueryableViewResponse> items, Func<QueryableViewResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());

        var source = Assert.Single((await CreateSut().Sut.GetForCallerAsync(cancellationToken: Cancel)).Queryables);

        Assert.Null(source.QueryUrl);
        Assert.Equal("ChartCard", Assert.Single(source.Views).Name);
    }

    [Fact]
    public async Task GetForCaller_MoreThanOneEntryProcessor_IsAConfigurationError()
    {
        responses.Setup(x => x.CreateRegistryResponse(It.IsAny<ProcessorRegistryItem>(), It.IsAny<KaleidoServiceOptions>()))
            .Returns((ProcessorRegistryItem _, KaleidoServiceOptions options) => new ProcessorRegistryResponse { Name = options.ServiceName, IsEntryProcessor = true });
        localProcessors.Add(new ProcessorRegistryItem());

        await Assert.ThrowsAsync<KaleidoConfigurationException>(() => CreateSut(aggregate: false).Sut.GetForCallerAsync(cancellationToken: Cancel));
    }

    [Fact]
    public void IsMapped_FollowsTheSnapshot()
    {
        Assert.True(CreateSut().Sut.IsMapped);
    }
}
