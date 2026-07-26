using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class RecordRegistryTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenServicesIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RecordRegistry(
                null!,
                [typeof(TestRecord)]));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenRecordTypesIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RecordRegistry(
                new ServiceCollection(),
                null!));
    }

    [Fact]
    public void Constructor_ShouldBuildRegistrations()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        Assert.Single(
            registry.Registrations);
    }

    [Fact]
    public void Registrations_ShouldContainRecordType()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            Assert.Single(
                registry.Registrations);

        Assert.Equal(
            typeof(TestRecord),
            registration.RecordType);
    }

    [Fact]
    public void Registrations_ShouldContainSourceType()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            Assert.Single(
                registry.Registrations);

        Assert.Equal(
            typeof(TestRecordSource),
            registration.SourceType);
    }

    [Fact]
    public void Registrations_ShouldContainMetadata()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            Assert.Single(
                registry.Registrations);

        Assert.Equal(
            "test-record",
            registration.Metadata.Name);

        Assert.Equal(
            "Test Record",
            registration.Metadata.Description);

        Assert.Equal(
            "Unit Test",
            registration.Metadata.Source);
    }

    [Fact]
    public void Registrations_ShouldContainFields()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.GetRegistration(
                typeof(TestRecord));

        Assert.Contains(
            registration.Metadata.Fields,
            x => x.Name == nameof(TestRecord.Name));

        Assert.Contains(
            registration.Metadata.Fields,
            x => x.Name == nameof(TestRecord.Amount));
    }

    [Fact]
    public void Registrations_ShouldContainNamedQueries()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.GetRegistration(
                typeof(TestRecord));

        var query =
            Assert.Single(
                registration.NamedQueryTypes);

        Assert.Equal(
            typeof(TestNamedQuery),
            query.NamedQueryType);

        Assert.Equal(
            "active",
            query.Metadata.Name);
    }

    [Fact]
    public void Registrations_ShouldContainNamedQueryParameters()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.GetRegistration(
                typeof(TestRecord));

        var query =
            Assert.Single(
                registration.NamedQueryTypes);

        var parameter =
            Assert.Single(
                query.Metadata.Parameters!);

        Assert.Equal(
            "Category",
            parameter.Name);

        Assert.Equal(
            typeof(string),
            parameter.Type);

        Assert.True(
            parameter.Required);
    }

    [Fact]
    public void GetAll_ShouldReturnRegistrations()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        Assert.Single(
            registry.GetAll());
    }

    [Fact]
    public void FindByName_ShouldReturnRegistration()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.Find(
                "test-record");

        Assert.NotNull(
            registration);
    }

    [Fact]
    public void FindByName_ShouldBeCaseInsensitive()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.Find(
                "TEST-RECORD");

        Assert.NotNull(
            registration);
    }

    [Fact]
    public void FindByName_ShouldReturnNull_WhenNotFound()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        Assert.Null(
            registry.Find(
                "missing"));
    }

    [Fact]
    public void FindByType_ShouldReturnRegistration()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.Find(
                typeof(TestRecord));

        Assert.NotNull(
            registration);
    }

    [Fact]
    public void FindByType_ShouldReturnNull_WhenNotFound()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        Assert.Null(
            registry.Find(
                typeof(UnknownRecord)));
    }

    [Fact]
    public void GetRegistrationByName_ShouldReturnRegistration()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.GetRegistration(
                "test-record");

        Assert.Equal(
            typeof(TestRecord),
            registration.RecordType);
    }

    [Fact]
    public void GetRegistrationByName_ShouldThrow_WhenNotFound()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        Assert.Throws<KeyNotFoundException>(
            () => registry.GetRegistration(
                "missing"));
    }

    [Fact]
    public void GetRegistrationByType_ShouldReturnRegistration()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        var registration =
            registry.GetRegistration(
                typeof(TestRecord));

        Assert.Equal(
            "test-record",
            registration.Metadata.Name);
    }

    [Fact]
    public void GetRegistrationByType_ShouldThrow_WhenNotFound()
    {
        var services = CreateServices();

        var registry =
            new RecordRegistry(
                services,
                [typeof(TestRecord)]);

        Assert.Throws<KeyNotFoundException>(
            () => registry.GetRegistration(
                typeof(UnknownRecord)));
    }

    private static IServiceCollection CreateServices()
    {
        var services =
            new ServiceCollection();

        services.AddScoped<
            IRecordSource<TestRecord>,
            TestRecordSource>();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            TestNamedQuery>();

        return services;
    }

    [KaleidoRecord(
        "test-record",
        "Test Record",
        null,
        "Unit Test")]
    [Pageable(50, 500)]
    private sealed record TestRecord(
        string Name,
        decimal Amount);

    private sealed record UnknownRecord;

    private sealed class TestRecordSource
        : IRecordSource<TestRecord>
    {
        public IQueryable<TestRecord> CreateQuery(
            RecordExecutionContext executionContext)
        {
            return Enumerable.Empty<TestRecord>()
                .AsQueryable();
        }
    }

    [NamedQuery(
        "active",
        "Active Records")]
    [NamedQueryParameter(
        "Category",
        typeof(string),
        Required = true,
        Description = "Category")]
    private sealed class TestNamedQuery
        : IRecordNamedQuery<TestRecord>
    {
        public IQueryable<TestRecord> Apply(IQueryable<TestRecord> query, KaleidoNamedQuery NamedQuery)
        {
            throw new NotImplementedException();
        }
    }
}