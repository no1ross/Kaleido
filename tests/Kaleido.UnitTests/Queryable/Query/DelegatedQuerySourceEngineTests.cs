using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;
using Kaleido.Registry;

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
        IServiceProvider serviceProvider,
        Mock<IQueryExecutionObservation>? observation = null,
        Mock<IKaleidoEventOutbox>? outbox = null)
    {
        var observability = new Mock<IQueryableObservability>();
        observability
            .Setup(x => x.BeginExecution(It.IsAny<QueryObservationDetails>()))
            .Returns(observation?.Object ?? Mock.Of<IQueryExecutionObservation>());

        return new(
            validator,
            Mock.Of<IQueryEventFactory>(),
            outbox?.Object ?? Mock.Of<IKaleidoEventOutbox>(),
            Mock.Of<IKaleidoCorrelationContextAccessor>(),
            observability.Object,
            serviceProvider);
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

    [Fact]
    public async Task ExecuteAsync_OnSuccess_RecordsMaterializationAndEnqueuesTheExecutedEvent()
    {
        var observation = new Mock<IQueryExecutionObservation>();
        var outbox = new Mock<IKaleidoEventOutbox>();

        await CreateSut(Mock.Of<IQueryContextValidator>(), ServiceProviderFor(new FakeDelegatedSource()), observation, outbox)
            .ExecuteAsync(new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid())), CreateRegistration());

        observation.Verify(x => x.BeginDelegate(), Times.Once);
        observation.Verify(x => x.Materialized(120, 1, 25, 25), Times.Once);
        outbox.Verify(x => x.EnqueueAsync(It.IsAny<KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_RecordsAValidationFailureOnly()
    {
        var failure = new KaleidoValidationException(QueryableErrorCodes.FieldNotFilterable, "Not filterable.");
        var validator = new Mock<IQueryContextValidator>();
        validator
            .Setup(x => x.Validate(It.IsAny<IQueryRequest>(), It.IsAny<QuerySourceMetadata>(), It.IsAny<PageableMetadata?>()))
            .Throws(failure);
        var observation = new Mock<IQueryExecutionObservation>();

        await Assert.ThrowsAsync<KaleidoValidationException>(() =>
            CreateSut(validator.Object, ServiceProviderFor(new FakeDelegatedSource()), observation)
                .ExecuteAsync(new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid())), CreateRegistration()));

        observation.Verify(x => x.ValidationFailed(failure), Times.Once);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheDownstreamIsCanceled_RecordsACancellationNotAFailure()
    {
        var source = new Mock<IDelegatedQuerySource<FakeContext, FakeResult, FakeParameters>>();
        source
            .Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest<FakeParameters>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromCanceled<QueryResult<FakeResult>>(new CancellationToken(canceled: true)));
        var observation = new Mock<IQueryExecutionObservation>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut(Mock.Of<IQueryContextValidator>(), ServiceProviderFor(source.Object), observation)
                .ExecuteAsync(new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid())), CreateRegistration()));

        observation.Verify(x => x.Canceled(), Times.Once);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheDownstreamFails_RecordsAFailure()
    {
        var failure = new HttpRequestException("downstream");
        var source = new Mock<IDelegatedQuerySource<FakeContext, FakeResult, FakeParameters>>();
        source
            .Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest<FakeParameters>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<QueryResult<FakeResult>>(failure));
        var observation = new Mock<IQueryExecutionObservation>();

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateSut(Mock.Of<IQueryContextValidator>(), ServiceProviderFor(source.Object), observation)
                .ExecuteAsync(new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid())), CreateRegistration()));

        Assert.Same(failure, thrown);
        observation.Verify(x => x.ExecutionFailed(failure), Times.Once);
        observation.Verify(x => x.Canceled(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheSourceReturnsNoTask_FailsWithTypeMismatch()
    {
        var source = new Mock<IDelegatedQuerySource<FakeContext, FakeResult, FakeParameters>>();
        source
            .Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest<FakeParameters>>(), It.IsAny<CancellationToken>()))
            .Returns((Task<QueryResult<FakeResult>>)null!);
        var observation = new Mock<IQueryExecutionObservation>();

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            CreateSut(Mock.Of<IQueryContextValidator>(), ServiceProviderFor(source.Object), observation)
                .ExecuteAsync(new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid())), CreateRegistration()));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
        observation.Verify(x => x.ExecutionFailed(exception), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheInstanceIsNotADelegatedSource_FailsWithTypeMismatch()
    {
        var observation = new Mock<IQueryExecutionObservation>();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateSut(Mock.Of<IQueryContextValidator>(), ServiceProviderFor(new object()), observation)
                .ExecuteAsync(new QueryRequest<FakeParameters>(new FakeParameters(Guid.NewGuid())), CreateRegistration()));

        var framework = exception as KaleidoFrameworkException ?? Assert.IsType<KaleidoFrameworkException>(exception.InnerException);
        Assert.Equal(FrameworkErrorCodes.TypeMismatch, framework.Code);
        Assert.Contains("must implement", framework.Message, StringComparison.Ordinal);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRequestIsNotTyped_FailsWithTypeMismatch()
    {
        var request = new Mock<IQueryRequest>();
        request.SetupGet(x => x.ViewParametersType).Returns(typeof(FakeParameters));
        var observation = new Mock<IQueryExecutionObservation>();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateSut(Mock.Of<IQueryContextValidator>(), ServiceProviderFor(new FakeDelegatedSource()), observation)
                .ExecuteAsync(request.Object, CreateRegistration()));

        var framework = exception as KaleidoFrameworkException ?? Assert.IsType<KaleidoFrameworkException>(exception.InnerException);
        Assert.Equal(FrameworkErrorCodes.TypeMismatch, framework.Code);
        Assert.Contains("expected request type", framework.Message, StringComparison.Ordinal);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Once);
    }

    private static IServiceProvider ServiceProviderFor(object source)
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
