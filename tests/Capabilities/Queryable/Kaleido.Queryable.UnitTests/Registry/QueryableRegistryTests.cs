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
            new KaleidoServiceOptions { ServiceName = "queryable", ServiceVersion = "1" },
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

        var sut = CreateSut(
            [new QuerySourceRegistration(typeof(LocalSource), typeof(object), typeof(object), typeof(EmptyQueryViewParameters), metadata)],
            [view]);
        var item = Assert.Single(sut.Registrations);

        Assert.Equal(phi, item.Fields.Single().Classifications);
        Assert.Equal(phi, item.Parameters.Single().Classifications);
        Assert.Equal(phi, item.OutputFields.Single().Classifications);
        Assert.Equal(phi, item.Views.Single().Parameters.Single().Classifications);
        Assert.Equal(phi, item.Views.Single().OutputFields.Single().Classifications);

        var published = Assert.Single(sut.Published);
        Assert.Equal(("queryable", "1", "LocalSource"), (published.ServiceName, published.ServiceVersion, published.Name));
        Assert.Equal(phi, Assert.Single(published.Fields).Classifications);
        Assert.Equal(phi, Assert.Single(published.Parameters).Classifications);
        Assert.Equal(phi, Assert.Single(published.OutputFields).Classifications);
        Assert.Equal(phi, Assert.Single(Assert.Single(published.Views).Parameters).Classifications);
        Assert.Equal(phi, Assert.Single(Assert.Single(published.Views).OutputFields).Classifications);
        Assert.Empty(published.Bindings.Http);
    }

    [Fact]
    public void Published_ViewPagingIsIndependentOfTheSource()
    {
        var registry = CreateSut(
            [Source(typeof(LocalSource), "LocalSource", QuerySourceKind.Local)],
            [new QueryViewRegistration(typeof(string), typeof(int), typeof(EmptyQueryViewParameters), typeof(LocalSource), typeof(object),
                new QueryViewMetadata("view", "View", "desc", new PageableMetadata(5, 50, "Id"), null, null))]);

        var published = Assert.Single(registry.Published);
        Assert.Null(published.Pageable);
        Assert.Equal(new QueryableRegistryPaging(5, 50, "Id"), Assert.Single(published.Views).Pageable);
    }

    [Fact]
    public void Published_PreservesNestedEnumTypesAndConstraintParameters()
    {
        var dataType = new DataTypeDescriptor("array", ItemType: new DataTypeDescriptor("string", "enum", Nullable: true,
            EnumValues: [new EnumValueDescriptor(2, "High", "Urgent")]));
        var metadata = new QuerySourceMetadata("DelegatedSource", "desc", "display", null, QuerySourceKind.Delegated, null,
            [new FieldMetadata { Name = "Priority", FieldType = typeof(string[]), DataType = dataType }],
            [new QueryParameterMetadata
            {
                Name = "Name",
                Type = typeof(string),
                DataType = new DataTypeDescriptor("string"),
                Constraints = [new ConstraintContract
                {
                    Type = "StringLength",
                    Parameters = [new ConstraintParameterContract { Name = "MaximumLength", Value = 10 }],
                }],
            }], [], AuthorizationMetadata.Unspecified);
        var registry = CreateSut(
            [new QuerySourceRegistration(typeof(DelegatedSource), typeof(object), typeof(object), typeof(EmptyQueryViewParameters), metadata)], []);

        var published = Assert.Single(registry.Published);
        var item = Assert.IsType<DataTypeMetadata>(Assert.Single(published.Fields).DataType.ItemType);
        Assert.Equal(("string", "enum", true), (item.Type, item.Format, item.Nullable));
        Assert.Equal(new EnumValueMetadata(2, "High", "Urgent"), Assert.Single(item.EnumValues!));
        var constraint = Assert.Single(Assert.Single(published.Parameters).Constraints);
        Assert.Equal("StringLength", constraint.Type);
        var parameter = Assert.Single(constraint.Parameters);
        Assert.Equal(("MaximumLength", 10), (parameter.Name, parameter.Value));
    }

    [Fact]
    public void Published_UsesStableStringTokensWithoutClrTypes()
    {
        var dataType = new DataTypeDescriptor("string");
        var metadata = new QuerySourceMetadata("LocalSource", "desc", "display", null, QuerySourceKind.Local, null,
            [new FieldMetadata
            {
                Name = "Name", FieldType = typeof(string), DataType = dataType, IsFilterable = true,
                FilterOperators = [FilterOperator.Equals, FilterOperator.NotEquals],
                IsSearchable = true, MatchMode = MatchMode.StartsWith,
            }], [], [], AuthorizationMetadata.Unspecified);
        var registry = CreateSut(
            [new QuerySourceRegistration(typeof(LocalSource), typeof(object), typeof(object), typeof(EmptyQueryViewParameters), metadata)], []);

        var published = Assert.Single(registry.Published);
        var field = Assert.Single(published.Fields);
        Assert.Equal(["equals", "notEquals"], field.FilterOperators);
        Assert.Equal("startsWith", field.MatchMode);
        Assert.Equal("string", field.DataType.Type);
        Assert.DoesNotContain(typeof(QueryableRegistryItem).GetProperties(), property => property.PropertyType == typeof(Type));
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
