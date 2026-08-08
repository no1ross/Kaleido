using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class RecordRegistrationValidatorTests
{
    private readonly RecordRegistrationValidator _validator = new();

    [Fact]
    public void Validate_ShouldThrow_WhenRecordTypesIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => _validator.Validate(
                null!,
                new ServiceCollection()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenServicesIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => _validator.Validate(
                [typeof(TestRecord)],
                null!));
    }

    [Fact]
    public void Validate_ShouldNotThrow_WhenRegistrationsAreValid()
    {
        var services = new ServiceCollection();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            ActiveQuery>();

        _validator.Validate(
            [typeof(TestRecord)],
            services);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenDuplicateRecordNamesExist()
    {
        var services = new ServiceCollection();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => _validator.Validate(
                    [
                        typeof(TestRecord),
                        typeof(DuplicateRecord)
                    ],
                    services));

        Assert.Contains(
            "Duplicate record names",
            exception.Message);
    }

    [Fact]
    public void Validate_ShouldTreatRecordNamesAsCaseInsensitive()
    {
        var services = new ServiceCollection();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => _validator.Validate(
                    [
                        typeof(TestRecord),
                        typeof(CaseInsensitiveDuplicateRecord)
                    ],
                    services));

        Assert.Contains(
            "Duplicate record names",
            exception.Message);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenDuplicateNamedQueriesExist()
    {
        var services = new ServiceCollection();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            ActiveQuery>();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            DuplicateActiveQuery>();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => _validator.Validate(
                    [typeof(TestRecord)],
                    services));

        Assert.Contains(
            nameof(TestRecord),
            exception.Message);
    }

    [Fact]
    public void Validate_ShouldTreatNamedQueryNamesAsCaseInsensitive()
    {
        var services = new ServiceCollection();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            ActiveQuery>();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            LowerCaseActiveQuery>();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => _validator.Validate(
                    [typeof(TestRecord)],
                    services));

        Assert.Contains(
            nameof(TestRecord),
            exception.Message);
    }

    [Fact]
    public void Validate_ShouldAllowMultipleUniqueNamedQueries()
    {
        var services = new ServiceCollection();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            ActiveQuery>();

        services.AddScoped<
            IRecordNamedQuery<TestRecord>,
            ByCategoryQuery>();

        _validator.Validate(
            [typeof(TestRecord)],
            services);
    }

    [Fact]
    public void Validate_ShouldAllowRecordsWithoutNamedQueries()
    {
        var services = new ServiceCollection();

        _validator.Validate(
            [typeof(TestRecord)],
            services);
    }

    [QueryableRecord(
        Name = "test-record",
        DisplayName = "Test Record",
        Version = "1.0.0",
        Source = "Unit Test")]
    private sealed record TestRecord;

    [QueryableRecord(
        Name = "test-record",
        DisplayName = "Duplicate",
        Version = "1.0.0",
        Source = "Unit Test")]
    private sealed record DuplicateRecord;

    [QueryableRecord(
        Name = "TEST-RECORD",
        DisplayName = "Duplicate",
        Version = "1.0.0",
        Source = "Unit Test")]
    private sealed record CaseInsensitiveDuplicateRecord;

    [NamedQuery(
        Name = "active",
        DisplayName = "Active Records")]
    private sealed class ActiveQuery
        : IRecordNamedQuery<TestRecord>
    {
        public IQueryable<TestRecord> Apply(IQueryable<TestRecord> query, NamedQuery NamedQuery)
        {
            throw new NotImplementedException();
        }
    }

    [NamedQuery(
        Name = "active",
        DisplayName = "Duplicate Active Records")]
    private sealed class DuplicateActiveQuery
        : IRecordNamedQuery<TestRecord>
    {
        public IQueryable<TestRecord> Apply(IQueryable<TestRecord> query, NamedQuery NamedQuery)
        {
            throw new NotImplementedException();
        }
    }

    [NamedQuery(
        Name = "ACTIVE",
        DisplayName = "Duplicate Active Records")]
    private sealed class LowerCaseActiveQuery
        : IRecordNamedQuery<TestRecord>
    {
        public IQueryable<TestRecord> Apply(IQueryable<TestRecord> query, NamedQuery NamedQuery)
        {
            throw new NotImplementedException();
        }
    }

    [NamedQuery(
        Name = "by-category",
        DisplayName = "By Category")]
    private sealed class ByCategoryQuery
        : IRecordNamedQuery<TestRecord>
    {
        public IQueryable<TestRecord> Apply(IQueryable<TestRecord> query, NamedQuery NamedQuery)
        {
            throw new NotImplementedException();
        }
    }
}