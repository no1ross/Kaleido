using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Registry.Http.Server.UnitTests;

public sealed class KaleidoRegistryServiceTests : Kaleido.UnitTests.SutFixture
{
    private static readonly AuthorizationMetadata Radiology = new(null, ["radiology"]);
    private static readonly DateTimeOffset GeneratedAt = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IKaleidoRegistrySource> source = new();
    private readonly Mock<IKaleidoAuthorizer> authorizer = new();

    public KaleidoRegistryServiceTests()
    {
        source.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Registry(
                [new ProcessRegistryItem { Name = "radiology", ServiceName = "radiology", Steps = [new ProcessStepResponse { Name = "CaptureScanStep", Authorization = Radiology }] }],
                [new QueryableRegistryItem { Name = "MemberSource", ServiceName = "member", Bindings = Http("/member/q") }]));
        authorizer.Setup(x => x.IsEnforced).Returns(false);
    }

    private KaleidoRegistryService CreateSut(
        ILogger<KaleidoRegistryService>? logger = null,
        IHttpContextAccessor? httpContextAccessor = null) =>
        new(source.Object, authorizer.Object, httpContextAccessor ?? Mock.Of<IHttpContextAccessor>(), logger ?? NullLogger<KaleidoRegistryService>.Instance);

    private static KaleidoRegistry Registry(
        IReadOnlyCollection<ProcessRegistryItem> processes,
        IReadOnlyCollection<QueryableRegistryItem> queryables,
        IReadOnlyCollection<RegistryReadError>? errors = null) =>
        new() { Processes = processes, Queryables = queryables, Errors = errors ?? [], Revision = "source-r1", GeneratedAt = GeneratedAt };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetForCaller_UsesOnlyTheFullCentralRegistry()
    {
        var registry = await CreateSut().GetForCallerAsync(refresh: false, Cancel);

        Assert.Equal(["radiology"], registry.Processes.Select(x => x.Name));
        Assert.Equal(["MemberSource"], registry.Queryables.Select(x => x.Name));
        Assert.Equal("source-r1", registry.Revision);
        Assert.Equal(GeneratedAt, registry.GeneratedAt);
        source.Verify(x => x.GetAsync(Cancel), Times.Once);
    }

    [Fact]
    public async Task GetForCaller_WhenRefreshed_AsksTheCentralSourceBeforeReading()
    {
        var sut = CreateSut();

        await sut.GetForCallerAsync(refresh: true, Cancel);

        source.Verify(x => x.RefreshAsync(Cancel), Times.Once);
        source.Verify(x => x.GetAsync(Cancel), Times.Once);
    }

    [Fact]
    public async Task GetForCaller_PreservesTheSourceRevision_WhenNotEnforced()
    {
        source.Setup(x => x.GetAsync(Cancel))
            .ReturnsAsync(Registry([], []) with { Revision = null });

        Assert.Null((await CreateSut().GetForCallerAsync(refresh: false, Cancel)).Revision);
    }

    [Fact]
    public async Task GetUnfiltered_ExposesTheFullRegistry_WithoutAskingTheAuthorizer()
    {
        authorizer.Setup(x => x.IsEnforced).Returns(true);

        var registry = await CreateSut().GetUnfilteredAsync(Cancel);

        Assert.Single(registry.Processes);
        Assert.Equal("source-r1", registry.Revision);
        authorizer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetForCaller_WhenEnforced_FiltersSteps_AndRevisesThePayload()
    {
        AllowEverything();
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> rule, CancellationToken _) =>
                items.Where(x => rule(x) != Radiology).ToArray());
        var sut = CreateSut();

        var filtered = await sut.GetForCallerAsync(refresh: false, Cancel);

        Assert.Empty(filtered.Processes);
        Assert.NotEqual("source-r1", filtered.Revision);
        Assert.Equal(filtered.Revision, (await sut.GetForCallerAsync(refresh: false, Cancel)).Revision);
    }

    [Fact]
    public async Task GetForCaller_WithoutAStableAuthenticatedSubject_DoesNotReuseAValidator()
    {
        AllowEverything();
        using var services = new ServiceCollection().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test")),
            RequestServices = services,
        };
        var caller = Mock.Of<IHttpContextAccessor>(x => x.HttpContext == context);
        var sut = CreateSut(httpContextAccessor: caller);

        var first = await sut.GetForCallerAsync(refresh: false, Cancel);
        var second = await sut.GetForCallerAsync(refresh: false, Cancel);

        Assert.NotEqual(first.Revision, second.Revision);
    }

    [Fact]
    public async Task GetForCaller_DoesNotExposeARouteOfAHiddenService()
    {
        source.Setup(x => x.GetAsync(Cancel)).ReturnsAsync(Registry(
            [new ProcessRegistryItem { Name = "radiology", ServiceName = "radiology", ServiceVersion = "1", Steps = [new ProcessStepResponse { Name = "CaptureScanStep", Authorization = Radiology }] }],
            [new QueryableRegistryItem { Name = "MemberSource", ServiceName = "member", ServiceVersion = "1", Bindings = Http("/member/1/q") }]) with
        {
            Routes =
            [
                new RegistryServiceRoute { ServiceName = "radiology", ServiceVersion = "1" },
                new RegistryServiceRoute { ServiceName = "member", ServiceVersion = "1" },
            ],
        });
        AllowEverything();
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> rule, CancellationToken _) => items.Where(x => rule(x) != Radiology).ToArray());

        var filtered = await CreateSut().GetForCallerAsync(refresh: false, Cancel);

        Assert.Empty(filtered.Processes);
        Assert.Equal("member", Assert.Single(filtered.Routes).ServiceName);
    }

    [Fact]
    public async Task GetForCaller_AnUnauthorizedLookup_IsRemovedButItsFieldStays()
    {
        source.Setup(x => x.GetAsync(Cancel)).ReturnsAsync(Registry(
            [new ProcessRegistryItem
            {
                Name = "clinic", ServiceName = "clinic",
                Steps = [new ProcessStepResponse { Name = "StartStep", Fields = [LookupField("MemberId", "member", "MemberSource"), LookupField("GhostId", "missing", "GhostSource")] }],
            }],
            [new QueryableRegistryItem { Name = "MemberSource", ServiceName = "member", Bindings = Http("/member/q") }]));
        AllowEverything();

        var step = Assert.Single(Assert.Single((await CreateSut().GetForCallerAsync(refresh: false, Cancel)).Processes).Steps!);
        var fields = step.Fields.ToDictionary(x => x.Name);

        Assert.NotNull(fields["MemberId"].Lookup);
        Assert.Null(fields["GhostId"].Lookup);
        Assert.Equal("pro_not_permitted", fields["GhostId"].Notice?.Code);
    }

    [Fact]
    public async Task GetForCaller_AnUnauthorizedSource_KeepsItsAllowedViewsButNoDirectUrl()
    {
        source.Setup(x => x.GetAsync(Cancel)).ReturnsAsync(Registry([], [new QueryableRegistryItem
        {
            Name = "ChartSource", ServiceName = "clinic", Bindings = Http("/clinic/q"), Authorization = Radiology,
            Views = [new QueryableRegistryViewItem { Name = "ChartCard", Bindings = Http("/clinic/v") }],
        }]));
        AllowEverything();
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthorizationMetadata? rule, CancellationToken _) => rule != Radiology);

        var chart = Assert.Single((await CreateSut().GetForCallerAsync(refresh: false, Cancel)).Queryables);

        Assert.Empty(chart.Bindings.Http);
        Assert.Equal("ChartCard", Assert.Single(chart.Views).Name);
    }

    [Fact]
    public async Task GetForCaller_MoreThanOneEntryProcessor_IsAConfigurationError()
    {
        source.Setup(x => x.GetAsync(Cancel)).ReturnsAsync(Registry(
            [new ProcessRegistryItem { Name = "clinic", IsEntryProcessor = true }, new ProcessRegistryItem { Name = "radiology", IsEntryProcessor = true }], []));

        await Assert.ThrowsAsync<KaleidoConfigurationException>(() => CreateSut().GetForCallerAsync(refresh: false, Cancel));
    }

    [Fact]
    public async Task GetForCaller_TwoVersionsOfOneEntryProcessor_AreOneProcessor()
    {
        source.Setup(x => x.GetAsync(Cancel)).ReturnsAsync(Registry(
            [new ProcessRegistryItem { Name = "clinic", ServiceVersion = "1", IsEntryProcessor = true },
             new ProcessRegistryItem { Name = "CLINIC", ServiceVersion = "2", IsEntryProcessor = true }], []));

        Assert.Equal(2, (await CreateSut().GetForCallerAsync(refresh: false, Cancel)).Processes.Count);
    }

    [Fact]
    public async Task GetForCaller_AFailedCentralRead_IsPartialWithoutInventingARevision()
    {
        source.Setup(x => x.GetAsync(Cancel)).ReturnsAsync(Registry([], [], [new RegistryReadError { Source = "registry", Reason = "down" }]) with { Revision = null });
        var logger = CreateLogger();

        var result = await CreateSut(logger.Object).GetForCallerAsync(refresh: false, Cancel);

        Assert.True(result.IsPartial);
        Assert.Null(result.Revision);
        Assert.Equal("registry", Assert.Single(result.Errors).Source);
        Assert.Contains(Logs(logger), x => x.Level == LogLevel.Warning && x.Message!.Contains("registry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetForCaller_CarriesMissingServices_AndIsPartial()
    {
        var missing = new RegistryMissingService { ServiceName = "clinic", Reason = "lookup" };
        source.Setup(x => x.GetAsync(Cancel)).ReturnsAsync(Registry([], []) with { Missing = [missing] });

        var result = await CreateSut().GetForCallerAsync(refresh: false, Cancel);

        Assert.Equal([missing], result.Missing);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public async Task GetForCaller_WhenEnforced_InitializesTheCurrentRequestCaller()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var services = new ServiceCollection();
        services.AddSingleton(initializer.Object);
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>(x => x.Current == new KaleidoCorrelationContext()));
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }) };
        AllowEverything();

        await CreateSut(httpContextAccessor: Mock.Of<IHttpContextAccessor>(x => x.HttpContext == context)).GetForCallerAsync(refresh: false, Cancel);

        initializer.Verify(x => x.Initialize(It.IsAny<KaleidoCorrelationContext>()), Times.Once);
    }

    private static RegistryTransportBindings Http(string path) => new()
    {
        Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = path }],
    };

    private static ProcessFieldMetadata LookupField(string name, string service, string source) =>
        new() { Name = name, DataType = new DataTypeMetadata("string"), Lookup = new ProcessFieldLookup { Service = service, Source = source, Field = "Id", Group = name } };

    private void AllowEverything()
    {
        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<ProcessStepResponse>>(), It.IsAny<Func<ProcessStepResponse, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ProcessStepResponse> items, Func<ProcessStepResponse, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IReadOnlyCollection<QueryableRegistryViewItem>>(), It.IsAny<Func<QueryableRegistryViewItem, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<QueryableRegistryViewItem> items, Func<QueryableRegistryViewItem, AuthorizationMetadata?> _, CancellationToken _) => items.ToArray());
    }

    private static Mock<ILogger<KaleidoRegistryService>> CreateLogger()
    {
        var logger = new Mock<ILogger<KaleidoRegistryService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    private static (LogLevel Level, string? Message)[] Logs(Mock<ILogger<KaleidoRegistryService>> logger) =>
        logger.Invocations
            .Where(x => string.Equals(x.Method.Name, "Log", StringComparison.Ordinal))
            .Select(x => ((LogLevel)x.Arguments[0], x.Arguments[2]?.ToString()))
            .ToArray();
}
