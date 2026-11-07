using System.Collections.Concurrent;
using Kaleido.Exceptions;
using Kaleido.Http.Server.Queryable;
using Kaleido.Platform.Queryable;
using Kaleido.Platform.Registry;
using Kaleido.Registry;
using Kaleido.Server.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.Server.Registry.UnitTests;

public sealed class KaleidoRegistryServiceTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly AuthorizationMetadata Radiology = new(null, ["radiology"]);

    private readonly ConcurrentDictionary<string, KaleidoRegistry> stored = new(StringComparer.OrdinalIgnoreCase);
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IKaleidoRegistrySource> source = new();
    private readonly Mock<IKaleidoAuthorizer> authorizer = new();
    private readonly Mock<IProcessorResponseFactory> responses = new();
    private readonly List<ProcessorRegistryItem> localProcessors = [new()];
    private readonly List<QueryableSourceRegistryItem> localSources = [];
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

        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents(
                [new ProcessorRegistryResponse { Name = "radiology", ServiceName = "radiology", Steps = [new ProcessStepResponse { Name = "CaptureScanStep", Authorization = Radiology }] }],
                [new QueryableSourceResponse { Name = "MemberSource", ServiceName = "member", QueryUrl = "/member/q" }],
                []));

        authorizer.Setup(x => x.IsEnforced).Returns(false);
    }

    private (KaleidoRegistryService Sut, RegistrySnapshot Snapshot) CreateSut(
        bool aggregate = true,
        TimeSpan? cooldown = null,
        ILogger<KaleidoRegistryService>? logger = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        // The cache's contract (its own rules are tested in HttpRegistryCacheTests): it serves what it
        // holds unless refreshed outside the cooldown, stamps what it builds, and keeps only a complete
        // result.
        var cache = new Mock<IHttpRegistryCache>();
        cache.Setup(c => c.GetOrBuildAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>(),
                It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string key, TimeSpan? _, TimeSpan refreshCooldown, bool forceRefresh, Func<CancellationToken, Task<KaleidoRegistry>> build, CancellationToken cancellationToken) =>
            {
                if (stored.TryGetValue(key, out var held) && (!forceRefresh || refreshCooldown > TimeSpan.Zero))
                {
                    return held;
                }

                var built = await build(cancellationToken) with { GeneratedAt = time.GetUtcNow() };
                built = built with { IsPartial = built.Errors.Count > 0 || built.Missing.Count > 0 };

                if (built.Errors.Count == 0)
                {
                    stored[key] = built;
                }

                return built;
            });

        var snapshot = new RegistrySnapshot(cache.Object, serviceOptions);
        snapshot.Configure(new KaleidoHttpMapOptions
        {
            AggregateRegistry = aggregate,
            RegistryRefreshCooldown = cooldown ?? TimeSpan.FromSeconds(30)
        });

        // This host's own capabilities, published as LocalRegistrations publishes them (its own tests).
        var local = new Mock<IKaleidoLocalCapabilities>();
        local.Setup(x => x.Capabilities()).Returns(() => new KaleidoRegistry
        {
            Processes = [.. localProcessors.Select(x => responses.Object.CreateRegistryResponse(x, serviceOptions))],
            Queryables = [.. localSources.Select(x => x.ToResponse(serviceOptions))],
        });

        return (
            new KaleidoRegistryService(
                snapshot,
                local.Object,
                source.Object,
                authorizer.Object,
                httpContextAccessor ?? Mock.Of<IHttpContextAccessor>(),
                logger ?? NullLogger<KaleidoRegistryService>.Instance),
            snapshot);
    }

    private static KaleidoRegistry Contents(IReadOnlyCollection<ProcessorRegistryResponse> processes, IReadOnlyCollection<QueryableSourceResponse> queryables, IReadOnlyCollection<RegistryReadError> errors) =>
        new() { Processes = processes, Queryables = queryables, Errors = errors };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetForCaller_MergesLocalAndDownstream_WhenAggregating()
    {
        var registry = await CreateSut().Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

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

        var first = await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);
        time.Advance(TimeSpan.FromSeconds(1));
        var rebuilt = await sut.GetForCallerAsync(refresh: true, cancellationToken: Cancel);

        Assert.NotEqual(first.GeneratedAt, rebuilt.GeneratedAt);
        Assert.Equal(first.Revision, rebuilt.Revision);
    }

    [Fact]
    public async Task GetForCaller_WithoutAggregation_ServesOnlyLocalRegistrations()
    {
        var registry = await CreateSut(aggregate: false).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.Equal(["router"], registry.Processes.Select(x => x.Name));
        source.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetForCaller_ServesTheSharedSnapshot_UntilRefreshed()
    {
        var (sut, _) = CreateSut(cooldown: TimeSpan.Zero);

        await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);
        await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);
        source.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);

        await sut.GetForCallerAsync(refresh: true, cancellationToken: Cancel);

        source.Verify(x => x.RefreshAsync(Cancel), Times.Once);
        source.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetForCaller_APartialAggregate_IsServedButNotKept()
    {
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents([], [], [new RegistryReadError { Source = "radiology", Reason = "down" }]));
        var (sut, _) = CreateSut();

        var first = await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);
        await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.True(first.IsPartial);
        Assert.Equal("radiology", Assert.Single(first.Errors).Source);
        source.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetForCaller_WhenEnforced_FiltersForTheCaller_AndTheRevisionFollowsTheContent()
    {
        var (sut, _) = CreateSut();
        var unfiltered = await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> rule, CancellationToken _) =>
                items.Where(x => rule(x) != Radiology).ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<QueryableViewResponse>>(), It.IsAny<Func<QueryableViewResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<QueryableViewResponse> items, Func<QueryableViewResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());

        var filtered = await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.Equal(["router"], filtered.Processes.Select(x => x.Name));
        Assert.NotEqual(unfiltered.Revision, filtered.Revision);
        Assert.Equal(filtered.Revision, (await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel)).Revision);
    }

    private static ProcessFieldMetadata LookupField(string name, string service, string source, string? view = null) =>
        new()
        {
            Name = name,
            DataType = new DataTypeMetadata("string"),
            Lookup = new ProcessFieldLookup { Service = service, Source = source, View = view, Field = "Id", Group = name },
        };

    // #524: a caller is never described a step or a query they may not use. A kept step's relationships
    // list only the steps the caller may run; an input whose lookup names a query the caller may not use
    // - by the rules the full registry publishes for it - keeps the input, without the lookup, with a
    // notice; a query found nowhere is not permitted.
    [Fact]
    public async Task GetForCaller_WhenEnforced_DescribesAStepsRelationshipsAndLookups_OnlyAsTheCallerMayUseThem()
    {
        responses.Setup(x => x.CreateRegistryResponse(It.IsAny<ProcessorRegistryItem>(), It.IsAny<KaleidoServiceOptions>()))
            .Returns((ProcessorRegistryItem _, KaleidoServiceOptions options) => new ProcessorRegistryResponse
            {
                Name = options.ServiceName,
                ServiceName = options.ServiceName,
                Steps =
                [
                    new ProcessStepResponse
                    {
                        Name = "CaptureMemberStep",
                        Dependencies = [new ProcessStepSummary { Name = "StartStep" }, new ProcessStepSummary { Name = "ApproveStep", Authorization = Radiology }],
                        AvailableAfter = [new ProcessStepSummary { Name = "ScanStep", Authorization = Radiology }],
                        AvailableUntil = [new ProcessStepSummary { Name = "CloseStep" }],
                        Fields =
                        [
                            LookupField("MemberId", "member", "MemberSource"),
                            LookupField("ScanId", "radiology", "ScanSource", "ScanCard"),
                            LookupField("ChartId", "clinic", "ChartSource"),
                            LookupField("GhostId", "nowhere", "GhostSource"),
                            new ProcessFieldMetadata { Name = "Note", DataType = new DataTypeMetadata("string") },
                        ],
                    },
                ],
            });
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents(
                [],
                [
                    new QueryableSourceResponse { Name = "MemberSource", ServiceName = "member", QueryUrl = "/member/q" },
                    new QueryableSourceResponse { Name = "ScanSource", ServiceName = "radiology", Views = [new QueryableViewResponse { Name = "ScanCard", QueryUrl = "/radiology/v", Authorization = Radiology }] },
                    new QueryableSourceResponse { Name = "ChartSource", ServiceName = "clinic", QueryUrl = "/clinic/q" },
                ],
                []));
        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthorizationMetadata? rule, CancellationToken _) => rule != Radiology);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> rule, CancellationToken _) => items.Where(x => rule(x) != Radiology).ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<QueryableViewResponse>>(), It.IsAny<Func<QueryableViewResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<QueryableViewResponse> items, Func<QueryableViewResponse, AuthorizationMetadata?> rule, CancellationToken _) => items.Where(x => rule(x) != Radiology).ToArray());
        var logger = new Mock<ILogger<KaleidoRegistryService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var registry = await CreateSut(logger: logger.Object).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        var step = Assert.Single(Assert.Single(registry.Processes, x => x.Name == "router").Steps!);
        Assert.Equal(["StartStep"], step.Dependencies.Select(x => x.Name));
        Assert.Empty(step.AvailableAfter);
        Assert.Equal(["CloseStep"], step.AvailableUntil.Select(x => x.Name));

        var fields = step.Fields.ToDictionary(x => x.Name);
        Assert.NotNull(fields["MemberId"].Lookup);
        Assert.NotNull(fields["ChartId"].Lookup);
        Assert.Null(fields["Note"].Notice);

        foreach (var denied in new[] { "ScanId", "GhostId" })
        {
            Assert.Null(fields[denied].Lookup);
            Assert.Equal(ProcessorErrorCodes.NotPermitted, fields[denied].Notice?.Code);
            Assert.DoesNotContain("Scan", fields[denied].Notice?.Message, StringComparison.Ordinal);
        }

        Assert.Equal(2, logger.Invocations.Count(x => x.Method.Name == nameof(ILogger.Log) && (LogLevel)x.Arguments[0] == LogLevel.Warning && x.Arguments[2].ToString()!.Contains("pro_not_permitted", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GetForCaller_ASourceTheCallerMayNotQueryDirectly_KeepsItsAllowedViews_WithoutAQueryUrl()
    {
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents(
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

        var chart = Assert.Single((await CreateSut().Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel)).Queryables);

        Assert.Null(chart.QueryUrl);
        Assert.Equal("ChartCard", Assert.Single(chart.Views).Name);
    }

    // #435: the MCP adapter names tools from the unfiltered registry, so what one caller may see never
    // changes a name; the authorizer is not consulted and nothing is removed.
    [Fact]
    public async Task GetUnfiltered_ReturnsTheWholeSnapshot_WithItsRevision_WhateverTheCallerMaySee()
    {
        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var (sut, _) = CreateSut();

        var unfiltered = await sut.GetUnfilteredAsync(Cancel);
        var again = await sut.GetUnfilteredAsync(Cancel);

        Assert.Contains(unfiltered.Processes, p => p.Steps!.Any(s => s.Name == "CaptureScanStep"));
        Assert.Contains(unfiltered.Queryables, q => q.Name == "MemberSource" && q.QueryUrl == "/member/q");
        Assert.False(string.IsNullOrEmpty(unfiltered.Revision));
        Assert.Equal(unfiltered.Revision, again.Revision);
        authorizer.Verify(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void EntryProcessors(params ProcessorRegistryResponse[] downstreamProcessors)
    {
        responses.Setup(x => x.CreateRegistryResponse(It.IsAny<ProcessorRegistryItem>(), It.IsAny<KaleidoServiceOptions>()))
            .Returns((ProcessorRegistryItem _, KaleidoServiceOptions options) => new ProcessorRegistryResponse { Name = options.ServiceName, IsEntryProcessor = true });
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents(downstreamProcessors, [], []));
    }

    [Fact]
    public async Task GetForCaller_MoreThanOneEntryProcessor_IsAConfigurationError()
    {
        EntryProcessors(new ProcessorRegistryResponse { Name = "radiology", IsEntryProcessor = true });

        await Assert.ThrowsAsync<KaleidoConfigurationException>(() => CreateSut().Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel));
    }

    // #553: the control registry lists each version of a service; the versions are one processor.
    [Fact]
    public async Task GetForCaller_VersionsOfTheEntryProcessor_AreOneEntryProcessor()
    {
        EntryProcessors(
            new ProcessorRegistryResponse { Name = "router", ServiceVersion = "1", IsEntryProcessor = true },
            new ProcessorRegistryResponse { Name = "ROUTER", ServiceVersion = "2", IsEntryProcessor = true });

        var registry = await CreateSut().Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.Equal(3, registry.Processes.Count);
    }

    [Fact]
    public void IsMapped_FollowsTheSnapshot()
    {
        Assert.True(CreateSut().Sut.IsMapped);
    }

    [Fact]
    public async Task GetForCaller_WhenBuilt_LogsTheCountsAtDebug()
    {
        var logger = CreateLogger();

        await CreateSut(logger: logger.Object).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        var log = Assert.Single(Logs(logger));
        Assert.Equal(LogLevel.Debug, log.Level);
        Assert.Equal("Registry response built: 2 process(es), 1 queryable(s).", log.Message);
    }

    [Fact]
    public async Task GetForCaller_WhenRefreshed_LogsTheRebuildAtDebug()
    {
        var logger = CreateLogger();
        var sut = CreateSut(cooldown: TimeSpan.Zero, logger: logger.Object).Sut;
        await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        await sut.GetForCallerAsync(refresh: true, cancellationToken: Cancel);

        Assert.Contains(Logs(logger), x => x.Level == LogLevel.Debug && x.Message == "Registry rebuilt via force refresh.");
    }

    [Fact]
    public async Task GetForCaller_WhenPartial_LogsTheFailedClientsOnceAtWarning()
    {
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents(
                [],
                [],
                [
                    new RegistryReadError { Source = "radiology", Reason = "down" },
                    new RegistryReadError { Source = "member", Reason = "down" },
                ]));
        var logger = CreateLogger();

        await CreateSut(logger: logger.Object).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        var log = Assert.Single(Logs(logger));
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Equal("Registry response is partial — 2 downstream client(s) failed or expected service(s) are missing: radiology, member.", log.Message);
    }

    [Fact]
    public async Task GetForCaller_WhenEnforced_InitializesTheCallerOfTheCurrentRequest()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var services = new ServiceCollection();
        services.AddSingleton(initializer.Object);
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>(x => x.Current == new KaleidoCorrelationContext()));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }),
        };
        var accessor = Mock.Of<IHttpContextAccessor>(x => x.HttpContext == httpContext);
        AllowEverything();

        await CreateSut(httpContextAccessor: accessor).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        initializer.Verify(x => x.Initialize(It.IsAny<KaleidoCorrelationContext>()), Times.Once);
    }

    [Fact]
    public async Task GetForCaller_WhenEnforced_DropsProcessorsAndSourcesTheCallerCannotUse()
    {
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents(
                [
                    new ProcessorRegistryResponse
                    {
                        Name = "radiology",
                        ServiceName = "radiology",
                        InitialSteps = [new ProcessStepSummary { Name = "OrderScanStep", Authorization = Radiology }],
                        Steps = [new ProcessStepResponse { Name = "CaptureScanStep", Authorization = Radiology }],
                    },
                    new ProcessorRegistryResponse
                    {
                        Name = "empty",
                        ServiceName = "empty",
                        Steps = null,
                    },
                    new ProcessorRegistryResponse
                    {
                        Name = "initial-only",
                        ServiceName = "initial-only",
                        InitialSteps = [new ProcessStepSummary { Name = "StartStep" }],
                        Steps = null,
                    },
                ],
                [
                    new QueryableSourceResponse
                    {
                        Name = "ScanSource",
                        ServiceName = "radiology",
                        QueryUrl = "/radiology/q",
                        Authorization = Radiology,
                        Views = [new QueryableViewResponse { Name = "ScanCard", QueryUrl = "/radiology/v", Authorization = Radiology }],
                    },
                    new QueryableSourceResponse { Name = "MemberSource", ServiceName = "member", QueryUrl = "/member/q" },
                ],
                []));
        localProcessors.Clear();
        var ruleChecks = new List<AuthorizationMetadata?>();
        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthorizationMetadata? rule, CancellationToken _) => rule != Radiology);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> rule, CancellationToken _) =>
                items.Where(x => Allowed(rule(x), ruleChecks)).ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> rule, CancellationToken _) =>
                items.Where(x => Allowed(rule(x), ruleChecks)).ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<QueryableViewResponse>>(), It.IsAny<Func<QueryableViewResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<QueryableViewResponse> items, Func<QueryableViewResponse, AuthorizationMetadata?> rule, CancellationToken _) =>
                items.Where(x => Allowed(rule(x), ruleChecks)).ToArray());

        var registry = await CreateSut().Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.Equal(["empty", "initial-only"], registry.Processes.Select(x => x.Name));
        Assert.Null(registry.Processes.First().Steps);
        Assert.Equal(["MemberSource"], registry.Queryables.Select(x => x.Name));
        Assert.Equal(3, ruleChecks.Count(x => x == Radiology));
    }

    private static bool Allowed(AuthorizationMetadata? rule, List<AuthorizationMetadata?> ruleChecks)
    {
        ruleChecks.Add(rule);
        return rule != Radiology;
    }

    private void AllowEverything()
    {
        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<QueryableViewResponse>>(), It.IsAny<Func<QueryableViewResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<QueryableViewResponse> items, Func<QueryableViewResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
    }

    private static Mock<ILogger<KaleidoRegistryService>> CreateLogger()
    {
        var logger = new Mock<ILogger<KaleidoRegistryService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    private static (LogLevel Level, string? Message)[] Logs(
        Mock<ILogger<KaleidoRegistryService>> logger) =>
        logger.Invocations
            .Where(x => string.Equals(x.Method.Name, "Log", StringComparison.Ordinal))
            .Select(x => ((LogLevel)x.Arguments[0], x.Arguments[2]?.ToString()))
            .ToArray();

    private static readonly RegistryMissingService MissingClinic = new() { ServiceName = "clinic", Reason = "lookup" };

    // #553: what a control registry is missing reaches the response, makes it partial, and is logged
    // with the rest.
    [Fact]
    public async Task GetForCaller_CarriesWhatTheSourceIsMissing_AndIsPartial()
    {
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KaleidoRegistry { Missing = [MissingClinic] });
        var logger = CreateLogger();

        var registry = await CreateSut(logger: logger.Object).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.Equal([MissingClinic], registry.Missing);
        Assert.True(registry.IsPartial);
        Assert.Contains(Logs(logger), x => x.Level == LogLevel.Warning && x.Message!.EndsWith("clinic.", StringComparison.Ordinal));
    }

    // #553: the revision is computed once, when the snapshot is built; an unfiltered read serves the
    // snapshot as it is, however many readers check it.
    [Fact]
    public async Task UnfilteredReads_ServeTheSnapshotAsBuilt_WithItsRevision()
    {
        var (sut, _) = CreateSut();

        var first = await sut.GetUnfilteredAsync(Cancel);
        var forCaller = await sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.Same(first, await sut.GetUnfilteredAsync(Cancel));
        Assert.Same(first, forCaller);
        Assert.False(string.IsNullOrEmpty(first.Revision));
    }

    // #553: a full registry (the control registry's, or a participant's copy of it) is the registry: it is
    // read whatever the map options, and served as it is - this host's own registrations are already in it.
    [Fact]
    public async Task GetForCaller_FromAFullRegistry_ServesIt_WithoutAddingThisHostsOwn()
    {
        source.Setup(x => x.IsFullRegistry).Returns(true);
        localSources.Add(new QueryableSourceRegistryItem
        {
            SourceType = typeof(object),
            QueryContextType = typeof(object),
            ResultType = typeof(object),
            ParametersType = typeof(object),
            Name = "RouterSource",
            DisplayName = "Router",
        });

        var registry = await CreateSut(aggregate: false).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel);

        Assert.Equal(["radiology"], registry.Processes.Select(x => x.Name));
        Assert.Equal(["MemberSource"], registry.Queryables.Select(x => x.Name));
        source.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // A full registry is complete: a lookup it does not have is not looked for anywhere else.
    [Fact]
    public async Task GetForCaller_FromAFullRegistry_ALookupItDoesNotHave_IsNotPermitted()
    {
        source.Setup(x => x.IsFullRegistry).Returns(true);
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents(
                [new ProcessorRegistryResponse
                {
                    Name = "radiology",
                    ServiceName = "radiology",
                    Steps = [new ProcessStepResponse { Name = "OrderScanStep", Fields = [LookupField("ChartId", "clinic", "ChartSource")] }],
                }],
                [],
                []));
        AllowEverything();

        var step = Assert.Single(Assert.Single((await CreateSut(aggregate: false).Sut.GetForCallerAsync(refresh: false, cancellationToken: Cancel)).Processes).Steps!);

        Assert.Null(Assert.Single(step.Fields).Lookup);
        source.Verify(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // #553: a full registry is held by its source with a revision - the snapshot is served while that
    // revision holds, rebuilt the moment it changes (whatever the cache's TTL), and rebuilt on every read
    // when the source has none (it could not read the registry).
    [Theory]
    [InlineData("r1", "r1", true)]
    [InlineData("r1", "r2", false)]
    [InlineData(null, null, false)]
    public async Task FromAFullRegistry_TheSnapshotFollowsTheSourcesRevision(string? first, string? second, bool same)
    {
        source.Setup(x => x.IsFullRegistry).Returns(true);
        var revisions = new Queue<string?>([first, second]);
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Contents([new ProcessorRegistryResponse { Name = "radiology", ServiceName = "radiology" }], [], []) with { Revision = revisions.Dequeue(), GeneratedAt = time.GetUtcNow() });
        var (sut, _) = CreateSut(aggregate: false);

        var before = await sut.GetUnfilteredAsync(Cancel);
        var after = await sut.GetUnfilteredAsync(Cancel);

        Assert.Equal(same, ReferenceEquals(before, after));
        Assert.False(string.IsNullOrEmpty(after.Revision));
        Assert.Equal(time.GetUtcNow(), after.GeneratedAt);
        Assert.Empty(stored);
    }

    // A full registry that could not be read is served partial, and a refresh asks the source to read now.
    [Fact]
    public async Task FromAFullRegistry_APartialRegistry_IsMarked_AndARefreshReadsAgain()
    {
        source.Setup(x => x.IsFullRegistry).Returns(true);
        source.Setup(x => x.GetAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Contents([], [], [new RegistryReadError { Source = "registry", Reason = "down" }]));

        var registry = await CreateSut(aggregate: false).Sut.GetForCallerAsync(refresh: true, cancellationToken: Cancel);

        Assert.True(registry.IsPartial);
        source.Verify(x => x.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
