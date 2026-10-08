using Kaleido.Registry;
using Kaleido.UnitTests;

namespace Kaleido.Queryable.Metadata.UnitTests;

public sealed class QuerySourceRegistrationTests
    : SutFixture<QuerySourceRegistration>
{
    private static readonly DataTypeDescriptor TestDataType =
        new("string");

    protected override QuerySourceRegistration CreateSut() =>
        CreateSut(null!);

    private static QuerySourceRegistration CreateSut(
        QuerySourceMetadata metadata) =>
        new(typeof(TestSource), typeof(TestContext), typeof(TestContext), typeof(EmptyQueryViewParameters), metadata);

    [Fact]
    public void QuerySourceRegistration_PreservesMetadataShape()
    {
        var field = new FieldMetadata("Code", "Code description", typeof(string), TestDataType, true, [FilterOperator.Equals], true, 1, MatchMode.Contains, true);
        var pageable = new PageableMetadata(25, 100);
        var metadata = new QuerySourceMetadata("TestSource", "Source description", "Source", "1.0.0", "Unit Test", QuerySourceKind.Local, pageable, [field], [], [], AuthorizationMetadata.Unspecified);
        var registration = CreateSut(metadata);

        Assert.Equal(typeof(TestSource), registration.SourceType);
        Assert.Equal(typeof(TestContext), registration.QueryContextType);
        Assert.Equal(typeof(TestContext), registration.ResultType);
        Assert.Equal(typeof(EmptyQueryViewParameters), registration.ParametersType);
        Assert.Equal(QuerySourceKind.Local, registration.Metadata.Kind);
        Assert.Equal(25, registration.Metadata.Pageable!.DefaultSize);
        Assert.Same(field, registration.Metadata.Fields.Single());
    }

    private sealed class TestContext
    {
    }

    private sealed class TestSource
    {
    }
}
