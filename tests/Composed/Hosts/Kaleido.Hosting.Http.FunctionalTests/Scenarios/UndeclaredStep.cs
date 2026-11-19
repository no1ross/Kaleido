namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

[ProcessStep(DisplayName = "Undeclared step", Description = "A step with no explicit authorization rule")]
public sealed record UndeclaredStep : IProcessStep;
