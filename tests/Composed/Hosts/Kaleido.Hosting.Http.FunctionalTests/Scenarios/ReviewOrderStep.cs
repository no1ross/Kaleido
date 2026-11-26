namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

[ProcessStep(DisplayName = "Review order", Description = "Review a local test order")]
[KaleidoAuthorization(Roles = "reviewer")]
public sealed record ReviewOrderStep : IProcessStep
{
    public required string Code { get; init; }
}
