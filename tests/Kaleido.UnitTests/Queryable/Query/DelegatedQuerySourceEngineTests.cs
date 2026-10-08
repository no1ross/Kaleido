using Kaleido.Registry;
using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class DelegatedQuerySourceEngineTests
    : Kaleido.UnitTests.SutFixture
{
    public sealed class FakeContext : IQueryContext;

    public sealed class FakeResult
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record FakeParameters(Guid ProcessId) : IQueryParameters;

    // Public (Moq proxies generic interfaces over these types) and therefore discoverable by
    // fixtures that scan this assembly, so it carries valid metadata.
    [QuerySource(Version = "1.0.0", DisplayName = "Fake delegated", Description = "Engine test delegated source.")]
    public sealed class FakeDelegatedSource : IDelegatedQuerySource<FakeContext, FakeResult, FakeParameters>
    {
        public IQueryRequest<FakeParameters>? ReceivedRequest { get; private set; }

        public Task<QueryResult<FakeResult>> ExecuteAsync(
            IQueryRequest<FakeParameters> request,
            CancellationToken cancellationToken = default)
        {
            ReceivedRequest = request;
            return Task.FromResult(new QueryResult<FakeResult>(120, 25, 25, [new FakeResult { Name = "Downstream" }]));
        }
    }

    private static DelegatedQuerySourceEngine<FakeContext, FakeResult> CreateSut(
        IQueryContextValidator validator,
        IServiceProvider serviceProvider)
    {
        var observability = new Mock<IQueryableObservability>();
        observability
            .Setup(x => x.BeginExecution(It.IsAny<QueryObservationDetails>()))
            .Returns(Mock.Of<IQueryExecutionObservation>());

        return new(
            validator,
            Mock.Of<IQueryEventFactory>(),
            Mock.Of<IEventPublisher>(),
            Mock.Of<IKaleidoCorrelationContextAccessor>(),
            observability.Object,
            serviceProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DelegatedQuerySourceEngine<FakeContext, FakeResult>>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ValidatesAgainstPublicShapeAndReturnsDownstreamResultUnchanged()
    {
        var source = new FakeDelegatedSource();
        var registration = CreateRegistration();
        var validator = new Mock<IQueryContextValidator>();
        var request = new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid()));

        var result = await CreateSut(validator.Object, ServiceProviderFor(source))
            .ExecuteAsync(request, registration);

        validator.Verify(x => x.Validate(request, registration.Metadata, registration.Metadata.Pageable), Times.Once);
        Assert.Same(request, source.ReceivedRequest);
        Assert.Equal(120, result.TotalCount);
        Assert.Equal(25, result.Offset);
        Assert.Equal("Downstream", Assert.Single(result.Results).Name);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_DoesNotCallSource()
    {
        var source = new FakeDelegatedSource();
        var validator = new Mock<IQueryContextValidator>();
        validator
            .Setup(x => x.Validate(It.IsAny<IQueryRequest>(), It.IsAny<QuerySourceMetadata>(), It.IsAny<PageableMetadata?>()))
            .Throws(new KaleidoValidationException(QueryableErrorCodes.FieldNotFilterable, "Not filterable."));

        await Assert.ThrowsAsync<KaleidoValidationException>(() =>
            CreateSut(validator.Object, ServiceProviderFor(source))
                .ExecuteAsync(new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid())), CreateRegistration()));

        Assert.Null(source.ReceivedRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WhenParametersTypeDiffers_Throws()
    {
        var exception =
            await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
                CreateSut(Mock.Of<IQueryContextValidator>(), ServiceProviderFor(new FakeDelegatedSource()))
                    .ExecuteAsync(new QueryRequest(), CreateRegistration()));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
    }

    private static IServiceProvider ServiceProviderFor(FakeDelegatedSource source)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeDelegatedSource))).Returns(source);
        return serviceProvider.Object;
    }

    private static QuerySourceRegistration CreateRegistration() =>
        new(
            typeof(FakeDelegatedSource),
            typeof(FakeContext),
            typeof(FakeResult),
            typeof(FakeParameters),
            new QuerySourceMetadata(
                nameof(FakeDelegatedSource),
                "Delegated source.",
                "Delegated",
                "1.0.0",
                null,
                QuerySourceKind.Delegated,
                new PageableMetadata(25, 250),
                [],
                [],
                [],
                AuthorizationMetadata.Unspecified));
}
