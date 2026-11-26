using static Kaleido.Registry.Server.UnitTests.RegistryTestData;

namespace Kaleido.Registry.Server.UnitTests;

// #553: an instance registers, heartbeats and deregisters; each is checked against who may register the
// service, the registry keeps one record per service version and revision, and a heartbeat or a
// deregistration is one conditional write.
public sealed class ControlRegistryTests
    : SutFixture
{
    private static readonly RegistryCaller RadiologyCaller = new("radiology-client", []);
    private static readonly RegistryCaller MemberCaller = new("member-client", []);
    private static readonly RegistryCaller Nobody = new(null, []);
    private static readonly string[] RadiologyOnly = ["radiology"];

    private readonly Mock<IKaleidoRegistryStore> store = new();
    private readonly Mock<IRegistryCache> cache = new();
    private readonly Mock<IRegistryRegistrants> registrants = new();
    private KaleidoServerOptions serverOptions = new();
    private readonly FakeTimeProvider time = new(Start);

    private static readonly KaleidoRegistryOptions Options = new();

    public ControlRegistryTests()
    {
        cache.Setup(x => x.InvalidateViewAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        registrants.Setup(x => x.MayRegister(RadiologyCaller, "radiology")).Returns(true);
        registrants.Setup(x => x.ServicesOf(It.IsAny<RegistryCaller>())).Returns([]);
        registrants.Setup(x => x.ServicesOf(RadiologyCaller)).Returns(RadiologyOnly);
        store.Setup(x => x.GetOrSetCurrentVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string version, CancellationToken _) => version);
    }

    private ControlRegistry CreateSut() => new(store.Object, cache.Object, Options, registrants.Object, serverOptions, time);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static RegistryInstanceRegistration Registration(string version = "2026-11", Guid? instance = null) =>
        new()
        {
            ServiceName = "radiology",
            ServiceVersion = version,
            BuildVersion = "1.0.0-preview.1",
            InstanceId = instance ?? Guid.NewGuid(),
            Processes = [ProcessorWith("radiology", "OrderScanStep")],
            Queryables = [SourceWith("radiology", "ScanSource")],
        };

    [Fact]
    public async Task Register_StoresTheVersionsRecord_TheInstance_AndTheCurrentVersion_AndLeasesIt()
    {
        RegistryRegistrationRecord? stored = null;
        KaleidoRegistry? published = null;
        RegistryInstanceRecord? instance = null;
        store.Setup(x => x.AddRegistrationAsync(It.IsAny<RegistryRegistrationRecord>(), It.IsAny<KaleidoRegistry>(), Cancel))
            .Callback((RegistryRegistrationRecord r, KaleidoRegistry c, CancellationToken _) => (stored, published) = (r, c))
            .Returns(Task.CompletedTask);
        store.Setup(x => x.SaveInstanceAsync(It.IsAny<RegistryInstanceRecord>(), Cancel)).Callback((RegistryInstanceRecord r, CancellationToken _) => instance = r).Returns(Task.CompletedTask);
        var registration = Registration();

        var answer = await CreateSut().RegisterAsync(registration, RadiologyCaller, Cancel);

        Assert.Equal(RegistryAnswerKind.Leased, answer.Kind);
        Assert.Equal(new RegistryLease { InstanceId = registration.InstanceId, HeartbeatInterval = Options.HeartbeatInterval, LeaseUntilUtc = Start + Options.Lease }, answer.Lease);
        Assert.NotNull(stored);
        Assert.NotNull(published);
        Assert.Equal(("radiology", "2026-11", "1.0.0-preview.1", Start), (stored.ServiceName, stored.ServiceVersion, stored.BuildVersion, stored.RegisteredUtc));
        var capabilities = published;
        Assert.Equal("2026-11", Assert.Single(capabilities.Processes).ServiceVersion);
        Assert.Equal("2026-11", Assert.Single(capabilities.Queryables).ServiceVersion);
        Assert.Equal(new RegistryTransportRoute { Transport = "http", GatewayRoute = "/radiology/2026-11" }, Assert.Single(Assert.Single(capabilities.Routes).Transports));
        Assert.Equal(new RegistryInstanceRecord { InstanceId = registration.InstanceId, ServiceName = "radiology", ServiceVersion = "2026-11", BuildVersion = stored.BuildVersion, RegisteredUtc = Start, LastHeartbeatUtc = Start }, instance);
        store.Verify(x => x.GetOrSetCurrentVersionAsync("radiology", "2026-11", Cancel), Times.Once);
        cache.Verify(x => x.InvalidateViewAsync(Cancel), Times.Once);
    }

    // Instances of the same build name one immutable publication: the store keeps one record for it.
    [Fact]
    public async Task Register_TwoInstancesOfOneBuild_ShareTheBuildVersion()
    {
        var builds = new List<string>();
        store.Setup(x => x.AddRegistrationAsync(It.IsAny<RegistryRegistrationRecord>(), It.IsAny<KaleidoRegistry>(), Cancel))
            .Callback((RegistryRegistrationRecord r, KaleidoRegistry _, CancellationToken _) => builds.Add(r.BuildVersion))
            .Returns(Task.CompletedTask);

        await CreateSut().RegisterAsync(Registration(), RadiologyCaller, Cancel);
        await CreateSut().RegisterAsync(Registration(), RadiologyCaller, Cancel);

        Assert.Single(builds.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Register_AChangedTransportBinding_UsesTheBuildVersionProvidedByTheService()
    {
        var builds = new List<string>();
        store.Setup(x => x.AddRegistrationAsync(It.IsAny<RegistryRegistrationRecord>(), It.IsAny<KaleidoRegistry>(), Cancel))
            .Callback((RegistryRegistrationRecord record, KaleidoRegistry _, CancellationToken _) => builds.Add(record.BuildVersion))
            .Returns(Task.CompletedTask);

        await CreateSut().RegisterAsync(Registration(), RadiologyCaller, Cancel);
        await CreateSut().RegisterAsync(Registration() with
        {
            BuildVersion = "1.0.0-preview.2",
            Transports =
            [
                new RegistryTransportRoute { Transport = "http", GatewayRoute = "/radiology/2026-11" },
                new RegistryTransportRoute { Transport = "grpc", GatewayRoute = "/grpc/radiology/2026-11" },
            ],
        }, RadiologyCaller, Cancel);

        Assert.Equal(["1.0.0-preview.1", "1.0.0-preview.2"], builds);
    }

    [Theory]
    [InlineData("http", "https://other.test/radiology/2026-11")]
    [InlineData("http", "//other.test/radiology/2026-11")]
    [InlineData("http", "/other/2026-11")]
    [InlineData("http", "/radiology/2026-11/extra")]
    [InlineData("http", "/radiology/%2e%2e/2026-11")]
    [InlineData("grpc", "/grpc/radiology/../2026-11")]
    [InlineData("other", "/radiology/2026-11")]
    public async Task Register_AnUnsafeOrUnknownTransportRoute_IsRefused(string transport, string route)
    {
        var registration = Registration() with
        {
            Transports = [new RegistryTransportRoute { Transport = transport, GatewayRoute = route }],
        };

        var answer = await CreateSut().RegisterAsync(registration, RadiologyCaller, Cancel);

        Assert.Equal((RegistryAnswerKind.Invalid, RegistryErrorCodes.InvalidRegistration), (answer.Kind, answer.Code));
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("step", "https://other.test/radiology/2026-11/processes/steps/scan")]
    [InlineData("source", "//other.test/radiology/2026-11/queryable/scan/query")]
    [InlineData("view", "/other/2026-11/queryable/scan/view/query")]
    [InlineData("state", "/radiology/2026-11/processes/../registry")]
    [InlineData("execute", "/radiology/2026-11/%2e%2e/registry")]
    public async Task Register_AnUnsafePublishedCapabilityUrl_IsRefused(string kind, string url)
    {
        var registration = Registration();
        var process = Assert.Single(registration.Processes);
        var query = Assert.Single(registration.Queryables);
        registration = kind switch
        {
            "step" => registration with { Processes = [process with { Steps = [new ProcessStepResponse { Name = "OrderScanStep", Bindings = Http("execute", "POST", url) }] }] },
            "source" => registration with { Queryables = [query with { Bindings = Http("query", "POST", url) }] },
            "view" => registration with { Queryables = [query with { Views = [new QueryableRegistryViewItem { Name = "ScanView", Bindings = Http("query", "POST", url) }] }] },
            "state" => registration with { Processes = [process with { Bindings = Http("state", "GET", url) }] },
            _ => registration with { Processes = [process with { Bindings = Http("execute", "POST", url) }] },
        };

        var answer = await CreateSut().RegisterAsync(registration, RadiologyCaller, Cancel);

        Assert.Equal((RegistryAnswerKind.Invalid, RegistryErrorCodes.InvalidRegistration), (answer.Kind, answer.Code));
        store.VerifyNoOtherCalls();
    }

    private static RegistryTransportBindings Http(string operation, string method, string path) => new()
    {
        Http = [new RegistryHttpOperation { Operation = operation, Method = method, Path = path }],
    };

    public static TheoryData<string> InvalidRegistrations() => ["no version", "bad version", "no build", "bad build", "bad name", "empty instance", "another service"];

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task Register_AnInvalidRegistration_IsRefused_AndNothingIsStored(string problem)
    {
        var registration = problem switch
        {
            "no version" => Registration(version: string.Empty),
            "bad version" => Registration(version: "-1"),
            "no build" => Registration() with { BuildVersion = string.Empty },
            "bad build" => Registration() with { BuildVersion = "-1" },
            "bad name" => Registration() with { ServiceName = "Radiology!" },
            "empty instance" => Registration(instance: Guid.Empty),
            _ => Registration() with { Queryables = [SourceWith("member", "MemberSource")] },
        };

        var answer = await CreateSut().RegisterAsync(registration, RadiologyCaller, Cancel);

        Assert.Equal((RegistryAnswerKind.Invalid, RegistryErrorCodes.InvalidRegistration), (answer.Kind, answer.Code));
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_ByACallerNotMappedToTheService_IsRefused(bool anonymous)
    {
        var answer = await CreateSut().RegisterAsync(Registration(), anonymous ? Nobody : MemberCaller, Cancel);

        Assert.Equal((RegistryAnswerKind.NotAllowed, RegistryErrorCodes.RegistrantNotAllowed), (answer.Kind, answer.Code));
        store.VerifyNoOtherCalls();
        cache.VerifyNoOtherCalls();
    }

    // AuthorizationMode None: nothing is enforced, development only.
    [Fact]
    public async Task WhenAuthorizationIsNotEnforced_AnyCallerMayRegister_RenewAndRemove()
    {
        serverOptions = new KaleidoServerOptions { AuthorizationMode = KaleidoAuthorizationMode.None };
        var id = Guid.NewGuid();
        store.Setup(x => x.RenewInstanceAsync(id, Start, true, null, Cancel)).ReturnsAsync(RegistryInstanceChange.Done);
        store.Setup(x => x.RemoveInstanceAsync(id, null, Cancel)).ReturnsAsync(RegistryInstanceChange.Done);

        Assert.Equal(RegistryAnswerKind.Leased, (await CreateSut().RegisterAsync(Registration(), registrant: Nobody, Cancel)).Kind);
        Assert.Equal(RegistryAnswerKind.Leased, (await CreateSut().HeartbeatAsync(id, new RegistryHeartbeat(), registrant: Nobody, Cancel)).Kind);
        Assert.Equal(RegistryAnswerKind.Removed, (await CreateSut().DeregisterAsync(id, registrant: Nobody, Cancel)).Kind);
    }

    // One conditional write, with the services the caller may register.
    [Fact]
    public async Task Heartbeat_RenewsTheLease_InOneConditionalWrite()
    {
        var id = Guid.NewGuid();
        time.Advance(TimeSpan.FromSeconds(10));
        store.Setup(x => x.RenewInstanceAsync(id, time.GetUtcNow(), false, RadiologyOnly, Cancel)).ReturnsAsync(RegistryInstanceChange.Done);

        var answer = await CreateSut().HeartbeatAsync(id, new RegistryHeartbeat { Healthy = false }, RadiologyCaller, Cancel);

        Assert.Equal(new RegistryLease { InstanceId = id, HeartbeatInterval = Options.HeartbeatInterval, LeaseUntilUtc = time.GetUtcNow() + Options.Lease }, answer.Lease);
        store.Verify(x => x.RenewInstanceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        cache.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(RegistryInstanceChange.Unknown, RegistryErrorCodes.InstanceUnknown)]
    [InlineData(RegistryInstanceChange.NotAllowed, RegistryErrorCodes.RegistrantNotAllowed)]
    public async Task HeartbeatAndDeregister_TheStoreRefusing_IsAnswered(RegistryInstanceChange change, string code)
    {
        var kind = change == RegistryInstanceChange.Unknown ? RegistryAnswerKind.Unknown : RegistryAnswerKind.NotAllowed;
        store.Setup(x => x.RenewInstanceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), It.IsAny<IReadOnlyCollection<string>?>(), Cancel)).ReturnsAsync(change);
        store.Setup(x => x.RemoveInstanceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<string>?>(), Cancel)).ReturnsAsync(change);

        var heartbeat = await CreateSut().HeartbeatAsync(Guid.NewGuid(), new RegistryHeartbeat(), MemberCaller, Cancel);
        var deregister = await CreateSut().DeregisterAsync(Guid.NewGuid(), MemberCaller, Cancel);

        Assert.Equal((kind, code), (heartbeat.Kind, heartbeat.Code));
        Assert.Equal((kind, code), (deregister.Kind, deregister.Code));
        store.Verify(x => x.RenewInstanceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<bool>(), Array.Empty<string>(), Cancel), Times.Once);
        cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Deregister_RemovesTheInstance_InOneConditionalWrite()
    {
        var id = Guid.NewGuid();
        store.Setup(x => x.RemoveInstanceAsync(id, RadiologyOnly, Cancel)).ReturnsAsync(RegistryInstanceChange.Done);

        var answer = await CreateSut().DeregisterAsync(id, RadiologyCaller, Cancel);

        Assert.Same(RegistryAnswer.Removed, answer);
        cache.Verify(x => x.InvalidateViewAsync(Cancel), Times.Once);
    }
}
