namespace Kaleido.Process.Http.Client.FunctionalTests.Consumer;

[ProcessStep(DisplayName = "Forward order", Description = "Hands off an order to the catalog service")]
[KaleidoAuthorization(Roles = "reviewer")]
public sealed record ForwardStep : IProcessStep
{
    public required string Code { get; init; }
}
