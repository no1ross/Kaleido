using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Abstractions.UnitTests;

public sealed class QueryMetadataTests
{
    [Fact]
    public void QueryContextRegistration_PreservesMetadataShape()
    {
        var field = new FieldMetadata("Code", "Code description", typeof(string), DataTypeMapper.GetDescriptor(typeof(string)), true, [FilterOperator.Equals], true, 1, MatchMode.Contains, true);
        var pageable = new PageableMetadata(25, 100);
        var metadata = new QueryContextMetadata("context", "Context description", "Context", "1.0.0", "Unit Test", QueryContextKind.Direct, pageable, [field]);
        var registration = new QueryContextRegistration(typeof(TestContext), typeof(TestSource), metadata);

        Assert.Equal(typeof(TestContext), registration.ContextType);
        Assert.Equal(typeof(TestSource), registration.SourceType);
        Assert.Equal(QueryContextKind.Direct, registration.Metadata.Kind);
        Assert.Equal(25, registration.Metadata.Pageable!.DefaultSize);
        Assert.Same(field, registration.Metadata.Fields.Single());
    }

    [Fact]
    public void QueryViewRegistration_PreservesMetadataShape()
    {
        var parameter = new QueryParameterMetadata("Category", typeof(string), DataTypeMapper.GetDescriptor(typeof(string)), [], "Category description");
        var outputField = new QueryOutputFieldMetadata("Code", "Code description", typeof(string), DataTypeMapper.GetDescriptor(typeof(string)));
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

    private sealed class TestContext
    {
    }

    private sealed class TestSource
    {
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
}
