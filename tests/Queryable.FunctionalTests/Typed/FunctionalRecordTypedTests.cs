using Kaleido.FunctionalTests.Fixtures;
using Kaleido.FunctionalTests.Infrastructure;
using Kaleido.Queryable;
using Kaleido.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.FunctionalTests.Typed;

public sealed class FunctionalRecordTypedTests : IClassFixture<FunctionalFixture>
{
    private readonly FunctionalFixture _fixture;

    public FunctionalRecordTypedTests(FunctionalFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void GetAll_Should_Return_Functional_Record_Metadata()
    {
        using var scope = _fixture.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IQueryableCatalog>();
        var metadata = catalog.GetRecordDescriptors();

        var record = Assert.Single(metadata, x => x.Key == "functional-records");

        Assert.Equal("functional-records", record.Key);
        Assert.Equal("SampleKaleidoRecord", record.Name);
        Assert.Equal("1.0.0", record.Version);
        Assert.Equal("CSV Functional Test Data", record.Source);
        Assert.NotEmpty(record.Fields);
        Assert.NotEmpty(record.AllowedQueries);
        Assert.NotNull(record.Pageable);
    }

    //[Fact]
    //public void Get_Should_Return_Functional_Record_Metadata()
    //{
    //    using var scope = _fixture.CreateScope();
    //    var catalog = scope.ServiceProvider.GetRequiredService<IQueryableCatalog>();
    //    var record = catalog.Get("functional-records");

    //    Assert.NotNull(record);
    //    Assert.Equal("functional-records", record!.Name);
    //    Assert.NotEmpty(record.Fields);
    //}

    [Theory]
    [MemberData(nameof(FunctionalScenarios.All), MemberType = typeof(FunctionalScenarios))]
    public async Task Typed_Query_Should_Produce_Expected_Result(FunctionalScenario scenario)
    {
        using var scope = _fixture.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IQueryableCatalog>();
        var data = scope.ServiceProvider.GetRequiredService<SampleKaleidoCsvData>();
        var request = scenario.CreateRequest(data.Records);

        var response = await catalog.QueryAsync<SampleKaleidoRecord>("functional-records", request);

        FunctionalAssertions.AssertScenarioResult(
            scenario,
            data.Records,
            new FunctionalQueryResult(response.Items, response.TotalCount));
    }
}
