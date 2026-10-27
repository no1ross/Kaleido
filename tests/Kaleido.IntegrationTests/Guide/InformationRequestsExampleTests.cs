using System.Net;
using System.Text.Json;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/information-requests.md, run: the round trip, and a conditional question.
public sealed class InformationRequestsExampleTests
{
    private static async Task<(Guid ProcessId, JsonElement Asked)> AskAsync(GuideHost host)
    {
        var placed = await host.StepAsync("PlaceOrderStep", new { item = "Book" });
        var processId = placed.GetProperty("processId").GetGuid();
        await host.StepAsync("ConfirmOrderStep", new { }, processId);

        return (processId, await host.StepAsync("GiftWrapStep", new { }, processId));
    }

    // The answers echo the exchange id the questions came with (#442).
    private static object Answers(JsonElement asked, params (string Item, string Value)[] answers) => new
    {
        informationRequestId = "gift-wrap",
        exchangeId = asked.GetProperty("requiredStep").GetProperty("informationRequest").GetProperty("exchangeId").GetString(),
        items = answers.Select(x => new { itemId = x.Item, answers = new[] { new { value = x.Value } } }).ToArray(),
    };

    [Fact]
    public async Task TheHandlerAsks_AndTheAnswersComeBackThroughTheInformationStep()
    {
        await using var host = await GuideHost.StartAsync();
        var (processId, asked) = await AskAsync(host);

        var required = asked.GetProperty("requiredStep");
        var answered = await host.StepAsync("GiftWrapAnswersStep", Answers(asked, ("wrap", "true"), ("paper", "gold")), processId);

        Assert.Equal("GiftWrapAnswersStep", required.GetProperty("name").GetString());
        Assert.Equal("gift-wrap", required.GetProperty("informationRequest").GetProperty("informationRequestId").GetString());
        Assert.Equal("completed", answered.GetProperty("outcome").GetString());
        Assert.Equal("gold", answered.GetProperty("result").GetProperty("paper").GetString());
    }

    [Fact]
    public async Task WhenTheConditionIsFalse_TheQuestionNeedsNoAnswer()
    {
        await using var host = await GuideHost.StartAsync();
        var (processId, asked) = await AskAsync(host);

        var answered = await host.StepAsync("GiftWrapAnswersStep", Answers(asked, ("wrap", "false")), processId);

        Assert.Equal("completed", answered.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, answered.GetProperty("result").GetProperty("paper").ValueKind);
    }

    // An answer to a question that is not enabled is not accepted: the step stays pending and the
    // same request is asked again; the handler never sees it.
    [Fact]
    public async Task AnAnswerToAQuestionThatIsNotEnabled_IsNotAccepted_AndTheRequestIsAskedAgain()
    {
        await using var host = await GuideHost.StartAsync();
        var (processId, asked) = await AskAsync(host);

        var (status, body) = await host.SendAsync(
            HttpMethod.Post,
            "/orders/processes/steps/GiftWrapAnswersStep",
            new { processStep = Answers(asked, ("wrap", "false"), ("paper", "red")) },
            processId);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("pending", body.GetProperty("outcome").GetString());
        Assert.Equal("gift-wrap", body.GetProperty("requiredStep").GetProperty("informationRequest").GetProperty("informationRequestId").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("result").ValueKind);
    }
}
