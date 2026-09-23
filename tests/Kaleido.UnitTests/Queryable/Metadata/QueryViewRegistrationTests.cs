namespace Kaleido.UnitTests.Queryable.Metadata;

public sealed class QueryViewRegistrationTests
{
    private static readonly DataTypeDescriptor TestDataType =
        new("string");

    [Fact]
    public void QueryViewRegistration_PreservesMetadataShape()
    {
        var parameter = new QueryParameterMetadata("Category", typeof(string), TestDataType, [], "Category description");
        var outputField = new QueryOutputFieldMetadata("Code", "Code description", typeof(string), TestDataType);
        var pageable = new PageableMetadata(10, 20);
        var metadata = new QueryViewMetadata("grid", "1.0.0", "Grid", "Grid description", QueryViewVisibility.Public, pageable, [parameter], [outputField]);
        var registration = new QueryViewRegistration(typeof(TestView), typeof(TestContract), typeof(TestParameters), typeof(TestContext), metadata);

        Assert.Equal(typeof(TestView), registration.QueryViewType);
        Assert.Equal(typeof(TestContract), registration.ViewType);
        Assert.Equal(typeof(TestParameters), registration.ViewParametersType);
        Assert.Equal(typeof(TestContext), registration.QueryContextType);
        Assert.Equal("grid", registration.Metadata.Name);
        Assert.Equal(10, registration.Metadata.Pageable!.DefaultSize);
        Assert.Same(parameter, registration.Metadata.Parameters!.Single());
    }

    private sealed class TestView
    {
    }

    private sealed class TestContract
    {
    }

    private sealed class TestParameters
    {
    }

    private sealed class TestContext
    {
    }
}
