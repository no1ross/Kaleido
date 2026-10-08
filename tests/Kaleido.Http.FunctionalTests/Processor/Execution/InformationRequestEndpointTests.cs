using System.Net;
using Kaleido.Http.FunctionalTests.Processor.Fixtures;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Processor;
using Kaleido.Http.Registry;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

// Framework messages (the pro_* codes) are hidden by default; rejection codes are covered by
// the planning and validator unit tests, and asserted here through outcome and state.
[Collection(nameof(ProcessorAspNetCoreSuite))]
public sealed class InformationRequestEndpointTests(ProcessorAspNetCoreFixture fixture)
{
    private const string AskUrl = "/kaleido/processes/steps/runtimeaskstep";
    private const string AnswerUrl = "/kaleido/processes/steps/runtimeanswerstep";

    [Fact]
    public async Task RequireInformation_ReturnsTheRequestOnTheRequiredStep_AndPersistsIt()
    {
        var processId = Guid.NewGuid();

        var asked = await PostAsync(AskUrl, new RuntimeAskStep(), processId);

        Assert.NotNull(asked.RequiredStep);
        Assert.Equal(RuntimeStepNames.Answer, asked.RequiredStep.Name);
        Assert.True(asked.RequiredStep.IsInformationStep);
        Assert.Equal("/kaleido/processes/steps/runtimeanswerstep", asked.RequiredStep.ExecuteUrl);
        Assert.Equal(RuntimeInformationRequests.FirstRound, asked.RequiredStep.InformationRequest?.InformationRequestId);
        Assert.Equal(InformationItemType.Choice, asked.RequiredStep.InformationRequest!.Items[2].Type);

        var state = await GetStateAsync(processId);

        Assert.Equal(ProcessExecutionState.AwaitingInformation, state.State);
        Assert.Equal(RuntimeInformationRequests.FirstRound, state.RequiredStep?.InformationRequest?.InformationRequestId);
    }

    [Fact]
    public async Task InformationRequest_UsesCamelCaseNamesAndEnumValues_OnTheWire()
    {
        var processId = Guid.NewGuid();
        using var request = CreateRequest(AskUrl, new RuntimeAskStep(), processId);

        using var response = await fixture.Client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"informationRequest\":{\"informationRequestId\":\"runtime-first-round\"", json);
        Assert.Contains("\"type\":\"choice\"", json);
        Assert.Contains("\"isInformationStep\":true", json);
    }

    [Fact]
    public async Task Answers_ThatFit_CompleteTheStep_AndClearThePendingRequest()
    {
        var processId = Guid.NewGuid();
        await PostAsync(AskUrl, new RuntimeAskStep(), processId);

        var answered = await PostAsync(AnswerUrl, Answers(RuntimeInformationRequests.FirstRound, more: false), processId);

        Assert.Equal(StepExecutionOutcome.Completed, answered.Outcome);
        Assert.Null(answered.RequiredStep);

        var state = await GetStateAsync(processId);
        Assert.NotEqual(ProcessExecutionState.AwaitingInformation, state.State);
        Assert.Null(state.RequiredStep);
    }

    [Fact]
    public async Task ARepeatableInformationStep_LoopsWithANewRequest_AndRejectsAnswersToTheOldOne()
    {
        var processId = Guid.NewGuid();
        await PostAsync(AskUrl, new RuntimeAskStep(), processId);

        var firstRound = await PostAsync(AnswerUrl, Answers(RuntimeInformationRequests.FirstRound, more: true), processId);

        Assert.Equal(StepExecutionOutcome.Completed, firstRound.Outcome);
        Assert.Equal(RuntimeStepNames.Answer, firstRound.RequiredStep?.Name);
        Assert.Equal(RuntimeInformationRequests.SecondRound, firstRound.RequiredStep?.InformationRequest?.InformationRequestId);

        var staleRound = await PostAsync(AnswerUrl, Answers(RuntimeInformationRequests.FirstRound, more: false), processId);

        Assert.NotEqual(StepExecutionOutcome.Completed, staleRound.Outcome);
        Assert.Equal(RuntimeInformationRequests.SecondRound, (await GetStateAsync(processId)).RequiredStep?.InformationRequest?.InformationRequestId);

        var secondRound = await PostAsync(AnswerUrl, Answers(RuntimeInformationRequests.SecondRound, more: false), processId);

        Assert.Equal(StepExecutionOutcome.Completed, secondRound.Outcome);
    }

    [Fact]
    public async Task Answers_ThatDoNotFit_AreRejected_AndTheRequestStaysPending()
    {
        var processId = Guid.NewGuid();
        await PostAsync(AskUrl, new RuntimeAskStep(), processId);

        var round = RuntimeInformationRequests.FirstRound;
        var rejected =
            await PostAsync(
                AnswerUrl,
                new RuntimeAnswerStep
                {
                    InformationRequestId = round,
                    Items = [Answer($"{round}-reason", "c")]
                },
                processId);

        Assert.NotEqual(StepExecutionOutcome.Completed, rejected.Outcome);

        var state = await GetStateAsync(processId);
        Assert.Equal(ProcessExecutionState.AwaitingInformation, state.State);
        Assert.Equal(round, state.RequiredStep?.InformationRequest?.InformationRequestId);
    }

    [Fact]
    public async Task AnotherStep_CannotBypassThePendingRequest()
    {
        var processId = Guid.NewGuid();
        await PostAsync(AskUrl, new RuntimeAskStep(), processId);

        var bypass = await PostAsync(AskUrl, new RuntimeAskStep(), processId);

        Assert.NotEqual(StepExecutionOutcome.Completed, bypass.Outcome);
        Assert.Equal(RuntimeStepNames.Answer, bypass.RequiredStep?.Name);
        Assert.Equal(ProcessExecutionState.AwaitingInformation, (await GetStateAsync(processId)).State);
    }

    [Fact]
    public async Task AnInformationStep_WithoutAPendingRequest_IsRejected()
    {
        var answered = await PostAsync(AnswerUrl, Answers(RuntimeInformationRequests.FirstRound, more: false), Guid.NewGuid());

        Assert.NotEqual(StepExecutionOutcome.Completed, answered.Outcome);
    }

    [Fact]
    public async Task Registry_MarksInformationSteps_AndPublishesNoFields()
    {
        var response = await fixture.Client.GetAsync("/kaleido/registry");
        var registry = await response.Content.ReadAsync<AggregatedRegistryResponse>();

        var processor = Assert.Single(registry!.Processes);
        var step = Assert.Single(processor.Steps!, x => x.Name == RuntimeStepNames.Answer);
        Assert.True(step.IsInformationStep);
        Assert.Empty(step.Fields);
        Assert.False(Assert.Single(processor.Steps!, x => x.Name == RuntimeStepNames.Ask).IsInformationStep);
    }

    private static RuntimeAnswerStep Answers(string round, bool more) =>
        new()
        {
            InformationRequestId = round,
            Items =
            [
                Answer($"{round}-more", more ? "true" : "false"),
                Answer($"{round}-reason", "a")
            ]
        };

    private static InformationResponseItem Answer(string itemId, string value) =>
        new()
        {
            ItemId = itemId,
            Answers = [new InformationAnswer { Value = value }]
        };

    private async Task<ProcessStateResponse> GetStateAsync(Guid processId)
    {
        using var response = await fixture.Client.GetAsync($"/kaleido/processes/{processId}");

        return await response.Content.ReadAsync<ProcessStateResponse>()
            ?? throw new InvalidOperationException("Empty state response.");
    }

    private async Task<StepExecutionResponse> PostAsync<TStep>(string url, TStep step, Guid processId)
        where TStep : IProcessStep
    {
        using var request = CreateRequest(url, step, processId);
        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsync<StepExecutionResponse>()
            ?? throw new InvalidOperationException("Empty step response.");
    }

    private static HttpRequestMessage CreateRequest<TStep>(string url, TStep step, Guid processId)
        where TStep : IProcessStep
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new ExecuteStepRequest<TStep> { ProcessStep = step })
        };
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString());

        return request;
    }
}
