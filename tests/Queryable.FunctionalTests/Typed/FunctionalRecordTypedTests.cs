using Kaleido.Queryable;
using Kaleido.Queryable.FunctionalTests.Fixtures;
using Kaleido.Queryable.FunctionalTests.Infrastructure;
using Kaleido.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.FunctionalTests.Typed;

public sealed class FunctionalRecordTypedTests
    : IClassFixture<FunctionalFixture>
{
    private readonly FunctionalFixture _fixture;

    public FunctionalRecordTypedTests(
        FunctionalFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void GetAll_Should_Return_Functional_Record_Metadata()
    {
        using var scope =
            _fixture.CreateScope();

        var catalog =
            scope.ServiceProvider
                .GetRequiredService<IQueryableCatalog>();

        var records =
            catalog.GetRecordDescriptors();

        var record =
            Assert.Single(
                records,
                x => x.Name == "functional-records");

        Assert.Equal(
            "functional-records",
            record.Name);

        Assert.Equal(
            "SampleKaleidoRecord",
            record.Description);

        Assert.Equal(
            "1.0.0",
            record.Version);

        Assert.Equal(
            "CSV Functional Test Data",
            record.Source);

        Assert.NotEmpty(
            record.Fields);

        Assert.NotNull(
            record.Pageable);
    }

    [Theory]
    [MemberData(
        nameof(FunctionalScenarios.All),
        MemberType = typeof(FunctionalScenarios))]
    public async Task Typed_Query_Should_Produce_Expected_Result(
        FunctionalScenario scenario)
    {
        using var scope =
            _fixture.CreateScope();

        var catalog =
            scope.ServiceProvider
                .GetRequiredService<IQueryableCatalog>();

        var data =
            scope.ServiceProvider
                .GetRequiredService<SampleKaleidoCsvData>();

        var request =
            scenario.CreateRequest(
                data.Records);

        var response =
            await catalog.QueryAsync<SampleKaleidoRecord>(
                "functional-records",
                request);

        FunctionalAssertions.AssertScenarioResult(
            scenario,
            data.Records,
            new FunctionalQueryResult(
                response.Items,
                response.TotalCount));
    }
}