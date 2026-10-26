using Kaleido.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Queryable.Registry.UnitTests;

public sealed class QueryableRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static QueryableRegistry CreateSut(
        IQuerySourceRegistry sourceRegistry,
        IQueryViewRegistry viewRegistry,
        Microsoft.Extensions.Logging.ILogger<QueryableRegistry>? logger = null) =>
        new(
            sourceRegistry,
            viewRegistry,
            logger ?? NullLogger<QueryableRegistry>.Instance);

    [Fact]
    public void Constructor_LogsTheBuiltRegistryOnceAtInformation()
    {
        var sourceRegistry = new Mock<IQuerySourceRegistry>();
        sourceRegistry.Setup(r => r.Registrations).Returns([Source(typeof(LocalSource), "LocalSource", QuerySourceKind.Local)]);
        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<QueryableRegistry>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);

        _ = CreateSut(sourceRegistry.Object, viewRegistry.Object, logger.Object);

        var logged = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(Microsoft.Extensions.Logging.ILogger.Log));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, logged.Arguments[0]);
        Assert.Equal("Queryable registry built with 1 sources.", logged.Arguments[2].ToString());
    }

    private static QueryableRegistry CreateSut(
        IReadOnlyCollection<QuerySourceRegistration> sources,
        IReadOnlyCollection<QueryViewRegistration> views)
    {
        var sourceRegistry = new Mock<IQuerySourceRegistry>();
        sourceRegistry.Setup(r => r.Registrations).Returns(sources);

        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(r => r.Registrations).Returns(views);

        return CreateSut(sourceRegistry.Object, viewRegistry.Object);
    }

    [Fact]
    public void Constructor_WhenSourceRegistryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateSut(null!, Mock.Of<IQueryViewRegistry>()));
    }

    [Fact]
    public void Constructor_WhenViewRegistryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateSut(Mock.Of<IQuerySourceRegistry>(), null!));
    }

    [Fact]
    public void Registrations_ProjectsLocalAndDelegatedSourcesTheSameWay()
    {
        var registry =
            CreateSut(
                [
                    Source(typeof(LocalSource), "LocalSource", QuerySourceKind.Local),
                    Source(typeof(DelegatedSource), "DelegatedSource", QuerySourceKind.Delegated, resultType: typeof(int), parametersType: typeof(long))
                ],
                []);

        Assert.Equal(2, registry.Registrations.Count);

        var delegated = Assert.Single(registry.Registrations, r => r.Name == "DelegatedSource");
        Assert.Equal(typeof(DelegatedSource), delegated.SourceType);
        Assert.Equal(typeof(int), delegated.ResultType);
        Assert.Equal(typeof(long), delegated.ParametersType);
        Assert.Empty(delegated.Views);
    }

    [Fact]
    public void Registrations_AreSortedAlphabetically()
    {
        var registry =
            CreateSut(
                [
                    Source(typeof(LocalSource), "zebra", QuerySourceKind.Local),
                    Source(typeof(DelegatedSource), "apple", QuerySourceKind.Delegated)
                ],
                []);

        Assert.Equal("apple", registry.Registrations.First().Name);
        Assert.Equal("zebra", registry.Registrations.Last().Name);
    }

    [Fact]
    public void Registrations_GroupsViewsUnderTheirSource()
    {
        var registry =
            CreateSut(
                [
                    Source(typeof(LocalSource), "LocalSource", QuerySourceKind.Local),
                    Source(typeof(OtherSource), "OtherSource", QuerySourceKind.Local)
                ],
                [
                    View(typeof(string), typeof(LocalSource), "b-view"),
                    View(typeof(int), typeof(LocalSource), "a-view"),
                    View(typeof(long), typeof(OtherSource), "other-view")
                ]);

        var local = Assert.Single(registry.Registrations, r => r.Name == "LocalSource");
        Assert.Equal(["a-view", "b-view"], local.Views.Select(v => v.Name));

        var other = Assert.Single(registry.Registrations, r => r.Name == "OtherSource");
        Assert.Equal("other-view", Assert.Single(other.Views).Name);
    }

    [Fact]
    public void Registrations_ProjectsViewClrTypes()
    {
        var registry =
            CreateSut(
                [Source(typeof(LocalSource), "LocalSource", QuerySourceKind.Local)],
                [
                    new QueryViewRegistration(
                        typeof(string),
                        typeof(int),
                        typeof(long),
                        typeof(LocalSource),
                        typeof(object),
                        new QueryViewMetadata("view1", "View", "desc", null, null, null))
                ]);

        var view = registry.Registrations.Single().Views.Single();
        Assert.Equal(typeof(string), view.QueryViewType);
        Assert.Equal(typeof(int), view.ViewType);
        Assert.Equal(typeof(long), view.ViewParametersType);
    }

    [Fact]
    public void Registrations_ViewWithoutAuthorization_InheritsSourceAuthorization()
    {
        var authorization = new AuthorizationMetadata("source-policy", ["internal"]);

        var registry =
            CreateSut(
                [Source(typeof(LocalSource), "LocalSource", QuerySourceKind.Local, authorization: authorization)],
                [View(typeof(string), typeof(LocalSource), "view1")]);

        var item = registry.Registrations.Single();
        Assert.Equal(authorization, item.Authorization);
        Assert.Equal(authorization, item.Views.Single().Authorization);
    }

    [Fact]
    public void Registrations_ViewWithAuthorization_KeepsOwnAuthorization()
    {
        var viewAuthorization = new AuthorizationMetadata("view-policy", ["clinician"]);

        var registry =
            CreateSut(
                [Source(typeof(LocalSource), "LocalSource", QuerySourceKind.Local, authorization: new AuthorizationMetadata("source-policy", ["internal"]))],
                [View(typeof(string), typeof(LocalSource), "view1", viewAuthorization)]);

        Assert.Equal(viewAuthorization, registry.Registrations.Single().Views.Single().Authorization);
    }

    [Fact]
    public void Registrations_PublishTheClassificationsOfFieldsParametersAndOutputs()
    {
        string[] phi = ["Test:PHI"];
        var dataType = new DataTypeDescriptor("string");
        var metadata = new QuerySourceMetadata(
            "LocalSource", "desc", "display", null, QuerySourceKind.Local, null,
            [new FieldMetadata { Name = "Name", FieldType = typeof(string), DataType = dataType, Classifications = phi }],
            [new QueryParameterMetadata { Name = "Ssn", Type = typeof(string), DataType = dataType, Classifications = phi }],
            [new QueryOutputFieldMetadata { Name = "Name", Type = typeof(string), DataType = dataType, Classifications = phi }],
            AuthorizationMetadata.Unspecified);
        var view = new QueryViewRegistration(
            typeof(string), typeof(object), typeof(EmptyQueryViewParameters), typeof(LocalSource), typeof(object),
            new QueryViewMetadata(
                "view1", "View", "desc", null,
                [new QueryParameterMetadata { Name = "Ssn", Type = typeof(string), DataType = dataType, Classifications = phi }],
                [new QueryOutputFieldMetadata { Name = "Name", Type = typeof(string), DataType = dataType, Classifications = phi }]));

        var item = CreateSut(
                [new QuerySourceRegistration(typeof(LocalSource), typeof(object), typeof(object), typeof(EmptyQueryViewParameters), metadata)],
                [view])
            .Registrations.Single();

        Assert.Equal(phi, item.Fields.Single().Classifications);
        Assert.Equal(phi, item.Parameters.Single().Classifications);
        Assert.Equal(phi, item.OutputFields.Single().Classifications);
        Assert.Equal(phi, item.Views.Single().Parameters.Single().Classifications);
        Assert.Equal(phi, item.Views.Single().OutputFields.Single().Classifications);
    }

    private static QuerySourceRegistration Source(
        Type sourceType,
        string name,
        QuerySourceKind kind,
        Type? resultType = null,
        Type? parametersType = null,
        AuthorizationMetadata? authorization = null) =>
        new(
            sourceType,
            typeof(object),
            resultType ?? typeof(object),
            parametersType ?? typeof(EmptyQueryViewParameters),
            new QuerySourceMetadata(name, "desc", "display", null, kind, null, [], [], [], authorization ?? AuthorizationMetadata.Unspecified));

    private static QueryViewRegistration View(
        Type viewType,
        Type sourceType,
        string name,
        AuthorizationMetadata? authorization = null) =>
        new(
            viewType,
            typeof(object),
            typeof(EmptyQueryViewParameters),
            sourceType,
            typeof(object),
            new QueryViewMetadata(name, name, "desc", null, null, null, authorization));

    private sealed class LocalSource;

    private sealed class OtherSource;

    private sealed class DelegatedSource;
}
