namespace Kaleido.Provider.FunctionalTests.Orders;

[ProcessStep(DisplayName = "Review order", Description = "Reviews the order for the in-memory provider host")]
public sealed record ReviewStep : IProcessStep
{
    public required string OrderId { get; init; }
}
