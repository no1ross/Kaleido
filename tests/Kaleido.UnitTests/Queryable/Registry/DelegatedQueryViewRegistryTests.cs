using System.Reflection;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;

namespace Kaleido.Queryable.Registry.UnitTests;

public sealed class DelegatedQueryViewRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static DelegatedQueryViewRegistry CreateSut(
        ITypeDescriber? TypeDescriber = null,
        IConstraintMapper? constraintMapper = null,
        IEnumerable<Type>? queryViewTypes = null,
        AuthorizationMetadata? defaultAuthorization = null)
    {
        var dtm = TypeDescriber ?? CreateDefaultTypeDescriber();
        var cm = constraintMapper ?? CreateDefaultConstraintMapper();

        return new DelegatedQueryViewRegistry(
            dtm, cm, queryViewTypes ?? [], defaultAuthorization ?? AuthorizationMetadata.Unspecified);
    }

    private static ITypeDescriber CreateDefaultTypeDescriber()
    {
        var mock = new Mock<ITypeDescriber>();
        mock.Setup(m => m.GetDescriptor(It.IsAny<PropertyInfo>()))
            .Returns(new DataTypeDescriptor("string"));
        return mock.Object;
    }

    private static IConstraintMapper CreateDefaultConstraintMapper()
    {
        var mock = new Mock<IConstraintMapper>();
        mock.Setup(m => m.Map(It.IsAny<PropertyInfo>()))
            .Returns([]);
        return mock.Object;
    }

    [Fact]
    public void Registrations_WhenEmpty_ReturnsEmptyCollection()
    {
        var sut = CreateSut();
        Assert.Empty(sut.Registrations);
    }

    [Fact]
    public void Find_ByName_WhenNotRegistered_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.Find("nonexistent"));
    }

    [Fact]
    public void Find_ByType_WhenNotRegistered_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.Find(typeof(object)));
    }

    [Fact]
    public void BuildRegistration_MapsAuthorizationFromViewAndContext()
    {
        var sut = CreateSut(queryViewTypes: [typeof(SecuredDelegatedView)]);

        var registration = sut.GetRegistration(typeof(SecuredDelegatedView));

        Assert.Equal("delegate-policy", registration.QueryMetadata.Authorization?.Policy);
        Assert.Equal("view-policy", registration.ViewMetadata.Authorization?.Policy);
    }

    [Fact]
    public void BuildRegistration_UndeclaredContext_InheritsServiceAuthorization()
    {
        var rule = new AuthorizationMetadata(null, ["radiology"]);
        var sut = CreateSut(
            queryViewTypes: [typeof(UnsecuredDelegatedView)],
            defaultAuthorization: rule);

        var registration = sut.GetRegistration(typeof(UnsecuredDelegatedView));

        Assert.Same(rule, registration.QueryMetadata.Authorization);
        Assert.Null(registration.ViewMetadata.Authorization);
    }

    [QueryContext(Name = "unsecured-context", Version = "1.0.0")]
    private sealed class UnsecuredContext;

    [QueryView(Name = "unsecured-delegated-view", Version = "1.0.0")]
    private sealed class UnsecuredDelegatedView
        : IDelegatedQueryViewSource<UnsecuredContext, DelegateContract>
    {
        public Task<QueryResult<DelegateContract>> ExecuteAsync(
            IQueryRequest<EmptyQueryViewParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<DelegateContract>(0, 0, 0, []));
    }

    [QueryContext(Name = "delegate-context", Version = "1.0.0")]
    [KaleidoAuthorization(Policy = "delegate-policy", Roles = "internal")]
    private sealed class DelegateContext;

    private sealed class DelegateContract;

    [QueryView(Name = "secured-delegated-view", Version = "1.0.0")]
    [KaleidoAuthorization(Policy = "view-policy")]
    private sealed class SecuredDelegatedView
        : IDelegatedQueryViewSource<DelegateContext, DelegateContract>
    {
        public Task<QueryResult<DelegateContract>> ExecuteAsync(
            IQueryRequest<EmptyQueryViewParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<DelegateContract>(0, 0, 0, []));
    }
}
