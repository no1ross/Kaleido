namespace Kaleido.Process.Http.Client.FunctionalTests.Publisher;

[ProcessStep(DisplayName = "Review order", Description = "Reviews a submitted order")]
[KaleidoAuthorization(Roles = "reviewer")]
public sealed record ReviewOrderStep : IProcessStep
{
    public required string Code { get; init; }
}
