using Kaleido.Queryable.Shared;
using Xunit;

namespace Kaleido.Queryable.FunctionalTests.Infrastructure;

public static class FunctionalAssertions
{
    public static void AssertScenarioResult(
        FunctionalScenario scenario,
        IReadOnlyList<SampleKaleidoRecord> allRecords,
        FunctionalQueryResult actual)
    {
        var expectedUnpaged =
            scenario.ExpectedUnpaged(allRecords);

        var expectedPaged =
            scenario.ExpectedPaged(allRecords);

        Assert.Equal(
            expectedUnpaged.Count,
            actual.TotalCount);

        Assert.Equal(
            expectedPaged.Count,
            actual.Items.Count);

        Assert.Equal(
            expectedPaged.Select(x => x.Id),
            actual.Items.Select(x => x.Id));

        for (var i = 0; i < expectedPaged.Count; i++)
        {
            AssertEquivalentRecord(
                expectedPaged[i],
                actual.Items[i]);
        }
    }

    private static void AssertEquivalentRecord(
        SampleKaleidoRecord expected,
        SampleKaleidoRecord actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.ExternalId, actual.ExternalId);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.IsActive, actual.IsActive);
        Assert.Equal(expected.Quantity, actual.Quantity);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.Rate, actual.Rate);
        Assert.Equal(expected.Score, actual.Score);
        Assert.Equal(expected.EffectiveDate, actual.EffectiveDate);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.ExpirationDate, actual.ExpirationDate);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.Region, actual.Region);
        Assert.Equal(expected.GroupName, actual.GroupName);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.NullableScore, actual.NullableScore);
    }
}