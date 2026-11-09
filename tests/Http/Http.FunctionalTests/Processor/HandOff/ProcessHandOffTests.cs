using Kaleido.Http.FunctionalTests.Authorization;
using Kaleido.Http.FunctionalTests.Processor.HandOff.Origin;
using Kaleido.Http.FunctionalTests.Processor.HandOff.Target;
using Kaleido.Http.Processor;
using Kaleido.Platform;
using Kaleido.Platform.Processor;

namespace Kaleido.Http.FunctionalTests.Processor.HandOff;

// #198: the request that hands off answers with the target's answer - its next steps as the target
// publishes them (with its URLs), its questions, its messages and its notice - and with the origin's
// own result. Each test is one kind of answer the target gives; a consumer behind a router follows
// the URLs and never needs to know the process moved.
public sealed class ProcessHandOffTests(HandOffFixture fixture)
    : IClassFixture<HandOffFixture>
{
    private const string User = "alice";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string TargetUrl(string stepName) =>
        ProcessContractUrls.ExecuteStep(HandOffFixture.TargetService, "1", stepName.ToLowerInvariant());

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object processStep, Guid? processId, string? roles)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { processStep }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.Add(TestAuthHandler.UserHeader, User);

        if (roles is not null)
        {
            request.Headers.Add(TestAuthHandler.RolesHeader, roles);
        }

        if (processId is { } id)
        {
            request.Headers.Add(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        return await client.SendAsync(request, Cancel);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response)
        {
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(KaleidoJsonOptions.Options, Cancel)
                ?? throw new InvalidOperationException("The response had no body.");
        }
    }

    private async Task<StepExecutionResponse<ReturnOrderResult>> OriginStepAsync(string next, Guid processId, string? roles) =>
        await ReadAsync<StepExecutionResponse<ReturnOrderResult>>(
            await PostAsync(fixture.Origin, ProcessContractUrls.ExecuteStep(HandOffFixture.OriginService, "1", nameof(ReturnOrderStep).ToLowerInvariant()), new { next }, processId, roles));

    // Places an order on the origin and returns it, asking the target to answer with next.
    private async Task<StepExecutionResponse<ReturnOrderResult>> ReturnAsync(string next, string? roles = null)
    {
        var placed = await ReadAsync<StepExecutionResponse>(
            await PostAsync(fixture.Origin, ProcessContractUrls.ExecuteStep(HandOffFixture.OriginService, "1", nameof(PlaceOrderStep).ToLowerInvariant()), new { }, processId: null, roles));

        return await OriginStepAsync(next, placed.ProcessId, roles);
    }

    [Fact]
    public async Task TheTargetsQuestions_ReachTheCaller_AndAreAnsweredOnTheTarget()
    {
        var handedOff = await ReturnAsync(ReturnAnswers.Questions);

        Assert.Equal(HandOffFixture.TargetService, handedOff.TargetProcessorName);
        var required = Assert.IsType<ProcessRequiredStep>(handedOff.RequiredStep);
        Assert.Equal(nameof(ReturnReasonStep), required.Name);
        Assert.Equal(TargetUrl(nameof(ReturnReasonStep)), required.ExecuteUrl);
        var questions = Assert.IsType<InformationRequest>(required.InformationRequest);
        Assert.Equal(ReturnAnswers.ReasonRequestId, questions.InformationRequestId);
        Assert.False(string.IsNullOrEmpty(questions.ExchangeId));
        Assert.Empty(handedOff.AvailableSteps);

        var answered = await ReadAsync<StepExecutionResponse>(await PostAsync(
            fixture.Target,
            required.ExecuteUrl,
            new
            {
                informationRequestId = questions.InformationRequestId,
                exchangeId = questions.ExchangeId,
                items = new[] { new { itemId = ReturnAnswers.ReasonItemId, answers = new[] { new { value = "It arrived broken." } } } },
            },
            handedOff.ProcessId,
            roles: null));

        Assert.Equal(StepExecutionStatus.Completed, answered.Status);
    }

    // The target's required step is described as the target publishes it, and running it runs on the
    // target; the origin's own result, built from the target's, comes with it.
    [Fact]
    public async Task AnOrdinaryRequiredStep_IsTheTargetsOwn_AndRunsOnTheTarget()
    {
        var handedOff = await ReturnAsync(ReturnAnswers.Ordinary);

        var required = Assert.IsType<ProcessRequiredStep>(handedOff.RequiredStep);
        Assert.Equal(nameof(InspectReturnStep), required.Name);
        Assert.Equal(ReturnAnswers.InspectDisplayName, required.DisplayName);
        Assert.Equal(TargetUrl(nameof(InspectReturnStep)), required.ExecuteUrl);
        Assert.Null(required.InformationRequest);
        Assert.Equal(ReturnAnswers.ReturnNumber, handedOff.Result?.ReturnNumber);

        var inspected = await ReadAsync<StepExecutionResponse>(await PostAsync(fixture.Target, required.ExecuteUrl, new { }, handedOff.ProcessId, roles: null));

        Assert.Equal(StepExecutionStatus.Completed, inspected.Status);
    }

    // The target's business messages are the handing-off step's.
    [Fact]
    public async Task TheTargetsMessages_AreTheStepsMessages()
    {
        var handedOff = await ReturnAsync(ReturnAnswers.Ordinary);

        Assert.Contains(handedOff.BusinessMessages, x => string.Equals(x.Code, ReturnAnswers.Opened.Code, StringComparison.Ordinal));
    }

    // A choice of the target's steps: each is checked for the caller, so a role-restricted one is
    // offered only to a caller with the role.
    [Fact]
    public async Task AChoiceOfTheTargetsSteps_OffersEachOnlyToACallerWhoMayRunIt()
    {
        var withRole = await ReturnAsync(ReturnAnswers.Choice, roles: ReturnAnswers.RefundRole);
        var withoutRole = await ReturnAsync(ReturnAnswers.Choice);

        Assert.Contains(withRole.AvailableSteps, x => string.Equals(x.Name, nameof(RefundStep), StringComparison.Ordinal));
        Assert.Contains(withoutRole.AvailableSteps, x => string.Equals(x.Name, nameof(InspectReturnStep), StringComparison.Ordinal) && string.Equals(x.ExecuteUrl, TargetUrl(nameof(InspectReturnStep)), StringComparison.Ordinal));
        Assert.DoesNotContain(withoutRole.AvailableSteps, x => string.Equals(x.Name, nameof(RefundStep), StringComparison.Ordinal));
        Assert.Null(withoutRole.Notice);
    }

    [Fact]
    public async Task ARequiredStepTheCallerMayNotRun_IsLeftOut_WithTheNotice()
    {
        var handedOff = await ReturnAsync(ReturnAnswers.Restricted);

        Assert.Null(handedOff.RequiredStep);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, handedOff.Notice?.Code);
    }

    [Fact]
    public async Task ATargetWithNothingNext_IsNamed_WithNoSteps()
    {
        var handedOff = await ReturnAsync(ReturnAnswers.Nothing);

        Assert.Equal(HandOffFixture.TargetService, handedOff.TargetProcessorName);
        Assert.Null(handedOff.RequiredStep);
        Assert.Empty(handedOff.AvailableSteps);
        Assert.Null(handedOff.Notice);
    }

    // The target refused: it did not take the process over, so the origin rejects the step with the
    // target's message and keeps the process.
    [Fact]
    public async Task WhenTheTargetRefuses_TheOriginRejects_AndKeepsTheProcess()
    {
        var refused = await ReturnAsync(ReturnAnswers.Reject);

        Assert.Equal(StepExecutionStatus.Rejected, refused.Status);
        Assert.Null(refused.TargetProcessorName);
        Assert.Contains(refused.BusinessMessages, x => string.Equals(x.Code, ReturnAnswers.Refused.Code, StringComparison.Ordinal));

        var state = await StateAsync(refused.ProcessId);
        Assert.Null(state.TargetProcessorName);
    }

    // After the hand-off the process is the target's: a later call to the origin runs none of its
    // steps, and the origin's state names the target.
    [Fact]
    public async Task ALaterCallToTheOrigin_IsRefused_AndItsStateNamesTheTarget()
    {
        var handedOff = await ReturnAsync(ReturnAnswers.Ordinary);

        var late = await OriginStepAsync(ReturnAnswers.Ordinary, handedOff.ProcessId, roles: null);

        Assert.Equal(StepExecutionStatus.Pending, late.Status);
        Assert.Contains(late.FrameworkMessages, x => string.Equals(x.Code, ProcessorErrorCodes.ProcessEnded, StringComparison.Ordinal));
        Assert.Equal(HandOffFixture.TargetService, (await StateAsync(handedOff.ProcessId)).TargetProcessorName);
    }

    private async Task<ProcessStateResponse> StateAsync(Guid processId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProcessContractUrls.ProcessState(HandOffFixture.OriginService, "1", processId));
        request.Headers.Add(TestAuthHandler.UserHeader, User);

        return await ReadAsync<ProcessStateResponse>(await fixture.Origin.SendAsync(request, Cancel));
    }
}
