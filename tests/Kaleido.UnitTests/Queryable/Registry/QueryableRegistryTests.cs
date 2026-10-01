using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.UnitTests.Queryable.Registry;

public sealed class QueryableRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static QueryableRegistry CreateSut(
        IQueryContextRegistry contextRegistry,
        IQueryViewRegistry viewRegistry,
        IDelegatedQueryViewRegistry delegatedRegistry) =>
        new(
            contextRegistry,
            viewRegistry,
            delegatedRegistry,
            NullLogger<QueryableRegistry>.Instance);

    [Fact]
    public void Constructor_WhenContextRegistryIsNull_Throws()
    {
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        Assert.Throws<ArgumentNullException>(() =>
            CreateSut(null!, viewRegistry.Object, delegatedRegistry.Object));
    }

    [Fact]
    public void Constructor_WhenViewRegistryIsNull_Throws()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var delegatedRegistry = Mock.Of<IDelegatedQueryViewRegistry>();

        Assert.Throws<ArgumentNullException>(() =>
            CreateSut(contextRegistry.Object, null!, delegatedRegistry));
    }

    [Fact]
    public void Constructor_WhenDelegatedRegistryIsNull_Throws()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = Mock.Of<IQueryViewRegistry>();

        Assert.Throws<ArgumentNullException>(() =>
            CreateSut(contextRegistry.Object, viewRegistry, null!));
    }

    [Fact]
    public void Registrations_ReturnsLocalRegistrations()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Single(registry.Registrations);
        Assert.Equal("context1", registry.Registrations.First().Name);
    }

    [Fact]
    public void Registrations_CombinesLocalAndDelegatedRegistrations()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []));

        var metadata = new QueryContextMetadata("delegated1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []);
        var delegatedRegistration = new DelegatedQueryViewRegistration(
            typeof(object),
            typeof(object),
            typeof(object),
            typeof(object),
            metadata,
            new QueryViewMetadata("view1", "desc", "display", "1.0", QueryViewVisibility.Public, null, null, null));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([delegatedRegistration]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Equal(2, registry.Registrations.Count);
        Assert.Contains(registry.Registrations, r => r.Name == "context1");
        Assert.Contains(registry.Registrations, r => r.Name == "delegated1");
    }

    [Fact]
    public void Registrations_AreSortedAlphabetically()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("zebra", "desc", "display", "1.0", null, QueryContextKind.Local, null, []));

        var metadata = new QueryContextMetadata("apple", "desc", "display", "1.0", null, QueryContextKind.Local, null, []);
        var delegatedRegistration = new DelegatedQueryViewRegistration(
            typeof(object),
            typeof(object),
            typeof(object),
            typeof(object),
            metadata,
            new QueryViewMetadata("view1", "desc", "display", "1.0", QueryViewVisibility.Public, null, null, null));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([delegatedRegistration]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Equal("apple", registry.Registrations.First().Name);
        Assert.Equal("zebra", registry.Registrations.Last().Name);
    }

    [Fact]
    public void Registrations_ProjectsContextType()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextRegistration = new QueryContextRegistration(
            typeof(string),
            typeof(object),
            new QueryContextMetadata("context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Equal(typeof(string), registry.Registrations.First().ContextType);
    }

    [Fact]
    public void Registrations_ProjectsViewCLRTypes()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []));

        var viewRegistration = new QueryViewRegistration(
            typeof(string),
            typeof(int),
            typeof(long),
            typeof(object),
            new QueryViewMetadata("view1", "desc", "display", "1.0", QueryViewVisibility.Public, null, null, null));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations)
            .Returns([viewRegistration]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var view = registry.Registrations.First().Views.Single();
        Assert.Equal(typeof(string), view.QueryViewType);
        Assert.Equal(typeof(int), view.ViewType);
        Assert.Equal(typeof(long), view.ViewParametersType);
    }

    [Fact]
    public void Registrations_ProjectsDelegatedViewCLRTypes()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        contextRegistry.Setup(r => r.Registrations).Returns([]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);

        var metadata = new QueryContextMetadata("delegated1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []);
        var delegatedRegistration = new DelegatedQueryViewRegistration(
            typeof(string),
            typeof(int),
            typeof(long),
            typeof(object),
            metadata,
            new QueryViewMetadata("view1", "desc", "display", "1.0", QueryViewVisibility.Public, null, null, null));

        delegatedRegistry.Setup(r => r.Registrations).Returns([delegatedRegistration]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var view = registry.Registrations.First().Views.Single();
        Assert.Equal(typeof(string), view.QueryViewType);
        Assert.Equal(typeof(int), view.ViewType);
        Assert.Equal(typeof(long), view.ViewParametersType);
    }

    [Fact]
    public void Registrations_ExcludesNonPublicViews()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []));

        var publicView = new QueryViewRegistration(
            typeof(object), typeof(object), typeof(object), typeof(object),
            new QueryViewMetadata("public-view", "1.0", "Public View", "desc", QueryViewVisibility.Public, null, null, null));

        var internalView = new QueryViewRegistration(
            typeof(object), typeof(object), typeof(object), typeof(object),
            new QueryViewMetadata("internal-view", "1.0", "Internal View", "desc", QueryViewVisibility.Internal, null, null, null));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([publicView, internalView]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var views = registry.Registrations.First().Views;
        Assert.Single(views);
        Assert.Equal("public-view", views.First().Name);
    }

    [Fact]
    public void Registrations_ViewWithoutAuthorization_InheritsContextAuthorization()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var authorization = new AuthorizationMetadata("context-policy", ["internal"]);

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, [], authorization));

        var viewRegistration = new QueryViewRegistration(
            typeof(object), typeof(object), typeof(object), typeof(object),
            new QueryViewMetadata("view1", "1.0", "View", "desc", QueryViewVisibility.Public, null, null, null));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([viewRegistration]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var item = registry.Registrations.Single();
        Assert.Equal(authorization, item.Authorization);
        Assert.Equal(authorization, item.Views.Single().Authorization);
    }

    [Fact]
    public void Registrations_ViewWithAuthorization_KeepsOwnAuthorization()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextAuthorization = new AuthorizationMetadata("context-policy", ["internal"]);
        var viewAuthorization = new AuthorizationMetadata("view-policy", ["clinician"]);

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, [], contextAuthorization));

        var viewRegistration = new QueryViewRegistration(
            typeof(object), typeof(object), typeof(object), typeof(object),
            new QueryViewMetadata("view1", "1.0", "View", "desc", QueryViewVisibility.Public, null, null, null, viewAuthorization));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([viewRegistration]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Equal(viewAuthorization, registry.Registrations.Single().Views.Single().Authorization);
    }

    [Fact]
    public void Registrations_DelegatedViewWithoutAuthorization_InheritsDelegateContextAuthorization()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var authorization = new AuthorizationMetadata("delegate-policy", ["internal"]);

        var metadata = new QueryContextMetadata("delegated1", "desc", "display", "1.0", null, QueryContextKind.Local, null, [], authorization);
        var delegatedRegistration = new DelegatedQueryViewRegistration(
            typeof(object), typeof(object), typeof(object), typeof(object),
            metadata,
            new QueryViewMetadata("view1", "desc", "display", "1.0", QueryViewVisibility.Public, null, null, null));

        contextRegistry.Setup(r => r.Registrations).Returns([]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([delegatedRegistration]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var item = registry.Registrations.Single();
        Assert.Equal(authorization, item.Authorization);
        Assert.Equal(authorization, item.Views.Single().Authorization);
    }

    [Fact]
    public void Registrations_FindByName_UsesLinq()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        var contextRegistration = new QueryContextRegistration(
            typeof(object),
            typeof(object),
            new QueryContextMetadata("Context1", "desc", "display", "1.0", null, QueryContextKind.Local, null, []));

        contextRegistry.Setup(r => r.Registrations).Returns([contextRegistration]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = CreateSut(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        // Lookup is now via Registrations — consumers use LINQ directly
        var result = registry.Registrations
            .FirstOrDefault(r => r.Name.Equals("CONTEXT1", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Equal("Context1", result.Name);
    }
}
