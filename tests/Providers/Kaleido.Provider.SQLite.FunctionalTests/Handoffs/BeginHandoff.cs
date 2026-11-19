namespace Kaleido.Provider.SQLite.FunctionalTests.Handoffs;

[ProcessStep(DisplayName = "Start hand-off", Description = "Hands a durable process to another service version")]
public sealed record BeginHandoff : IProcessStep
{
    public required string OrderId { get; init; }
}
