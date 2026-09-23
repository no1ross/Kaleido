namespace Kaleido.UnitTests.Queryable.Metadata;

public sealed class QueryContextRegistrationTests
{
    private static readonly DataTypeDescriptor TestDataType =
        new("string");

    [Fact]
    public void QueryContextRegistration_PreservesMetadataShape()
    {
        var field = new FieldMetadata("Code", "Code description", typeof(string), TestDataType, true, [FilterOperator.Equals], true, 1, MatchMode.Contains, true);
        var pageable = new PageableMetadata(25, 100);
        var metadata = new QueryContextMetadata("context", "Context description", "Context", "1.0.0", "Unit Test", QueryContextKind.Direct, pageable, [field]);
        var registration = new QueryContextRegistration(typeof(TestContext), typeof(TestSource), metadata);

        Assert.Equal(typeof(TestContext), registration.ContextType);
        Assert.Equal(typeof(TestSource), registration.SourceType);
        Assert.Equal(QueryContextKind.Direct, registration.Metadata.Kind);
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
