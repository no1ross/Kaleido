using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Records;
using Moq;
using Xunit;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class QueryableRegistryTests
{
    [Fact]
    public void Constructor_WhenContextRegistryIsNull_Throws()
    {
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        Assert.Throws<ArgumentNullException>(() =>
            new QueryableRegistry(null!, viewRegistry.Object, delegatedRegistry.Object));
    }

    [Fact]
    public void Constructor_WhenViewRegistryIsNull_Throws()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var delegatedRegistry = Mock.Of<IDelegatedQueryViewRegistry>();

        Assert.Throws<ArgumentNullException>(() =>
            new QueryableRegistry(contextRegistry.Object, null!, delegatedRegistry));
    }

    [Fact]
    public void Constructor_WhenDelegatedRegistryIsNull_Throws()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = Mock.Of<IQueryViewRegistry>();

        Assert.Throws<ArgumentNullException>(() =>
            new QueryableRegistry(contextRegistry.Object, viewRegistry, null!));
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

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

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

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

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

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Equal("apple", registry.Registrations.First().Name);
        Assert.Equal("zebra", registry.Registrations.Last().Name);
    }

    [Fact]
    public void Find_WhenNameIsNull_Throws()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        contextRegistry.Setup(r => r.Registrations).Returns([]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Throws<ArgumentNullException>(() =>
            registry.Find(null!));
    }

    [Fact]
    public void Find_WhenNameIsWhitespace_Throws()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        contextRegistry.Setup(r => r.Registrations).Returns([]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Throws<ArgumentException>(() =>
            registry.Find("   "));
    }

    [Fact]
    public void Find_WhenRegistrationExists_ReturnsRegistration()
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

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var result = registry.Find("context1");

        Assert.NotNull(result);
        Assert.Equal("context1", result.Name);
    }

    [Fact]
    public void Find_WhenRegistrationNotFound_ReturnsNull()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        contextRegistry.Setup(r => r.Registrations).Returns([]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var result = registry.Find("unknown");

        Assert.Null(result);
    }

    [Fact]
    public void Find_UsesCaseInsensitiveMatching()
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

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var result = registry.Find("CONTEXT1");

        Assert.NotNull(result);
        Assert.Equal("Context1", result.Name);
    }

    [Fact]
    public void GetRegistration_WhenRegistrationExists_ReturnsRegistration()
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

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        var result = registry.GetRegistration("context1");

        Assert.NotNull(result);
        Assert.Equal("context1", result.Name);
    }

    [Fact]
    public void GetRegistration_WhenRegistrationNotFound_Throws()
    {
        var contextRegistry = new Mock<IQueryContextRegistry>();
        var viewRegistry = new Mock<IQueryViewRegistry>();
        var delegatedRegistry = new Mock<IDelegatedQueryViewRegistry>();

        contextRegistry.Setup(r => r.Registrations).Returns([]);
        viewRegistry.Setup(r => r.Registrations).Returns([]);
        delegatedRegistry.Setup(r => r.Registrations).Returns([]);

        var registry = new QueryableRegistry(contextRegistry.Object, viewRegistry.Object, delegatedRegistry.Object);

        Assert.Throws<KeyNotFoundException>(() =>
            registry.GetRegistration("unknown"));
    }
}
