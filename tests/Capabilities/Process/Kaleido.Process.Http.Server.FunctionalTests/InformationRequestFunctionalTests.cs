using System.Net;
using System.Net.Http.Json;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.FunctionalTests.Information;

public sealed class InformationRequestFunctionalTests
{
    private const string AskUrl = "/catalog/1/processes/steps/askquestionsstep";
    private const string AnswerUrl = "/catalog/1/processes/steps/answerquestionsstep";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequireInformation_PublishesTheConditionalRequestAndPersistsItsExchange()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        using var response = await SendAsync(client, AskUrl, new AskQuestionsStep(), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("\"requiredInformationRequest\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"choice\"", json, StringComparison.Ordinal);
        Assert.Contains("\"enableWhen\"", json, StringComparison.Ordinal);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        var request = Assert.IsType<InformationRequest>(result.RequiredInformationRequest);
        Assert.Equal(nameof(AnswerQuestionsStep), result.RequiredStep);
        Assert.Equal(Questionnaires.FirstRound, request.InformationRequestId);
        Assert.Matches("^[0-9a-f]{32}$", request.ExchangeId);
        Assert.Equal(InformationItemType.Choice, request.Items.Single(item => item.Id.EndsWith("-reason", StringComparison.Ordinal)).Type);

        var state = await GetStateAsync(client, result.ProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingInformation, state.State);
        Assert.Equal(request.ExchangeId, state.RequiredInformationRequest?.ExchangeId);
        Assert.Equal(nameof(AnswerQuestionsStep), state.RequiredStep);
    }

    [Fact]
    public async Task ValidAnswers_CompleteTheStepAndClearThePendingRequest()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, request) = await StartAskingAsync(client);

        var answered = await PostAsync(client, AnswerUrl, Answers(request, more: false), processId);

        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(answered.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Null(answered.RequiredStep);
        Assert.Null(answered.RequiredInformationRequest);
        var state = await GetStateAsync(client, processId);
        Assert.NotEqual(ProcessExecutionState.AwaitingInformation, state.State);
        Assert.Null(state.RequiredInformationRequest);
    }

    [Fact]
    public async Task RepeatableQuestionnaire_RejectsAnEarlierExchangeEvenWhenItsQuestionsMatch()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, first) = await StartAskingAsync(client);
        var accepted = Answers(first, more: true, reason: "again");
        var again = await PostAsync(client, AnswerUrl, accepted, processId);
        var second = Assert.IsType<InformationRequest>(again.RequiredInformationRequest);
        Assert.Equal(first.InformationRequestId, second.InformationRequestId);
        Assert.NotEqual(first.ExchangeId, second.ExchangeId);

        var replayed = await PostAsync(client, AnswerUrl, accepted, processId);
        Assert.NotEqual(StepExecutionStatus.Completed, Assert.Single(replayed.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Equal(second.ExchangeId, (await GetStateAsync(client, processId)).RequiredInformationRequest?.ExchangeId);

        var current = await PostAsync(client, AnswerUrl, Answers(second, more: false), processId);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(current.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Null((await GetStateAsync(client, processId)).RequiredInformationRequest);
    }

    [Fact]
    public async Task ConditionalQuestion_IsRequiredOnlyWhenTheAnswerEnablesIt()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, request) = await StartAskingAsync(client);
        var missing = Answers(request, more: false, reason: "b");

        var rejected = await PostAsync(client, AnswerUrl, missing, processId);
        Assert.NotEqual(StepExecutionStatus.Completed, Assert.Single(rejected.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Equal(ProcessExecutionState.AwaitingInformation, (await GetStateAsync(client, processId)).State);

        var complete = await PostAsync(client, AnswerUrl, missing with
        {
            Items = [.. missing.Items, Answer($"{request.InformationRequestId}-detail", "Because")],
        }, processId);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(complete.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
    }

    [Fact]
    public async Task InvalidChoice_DoesNotClearThePendingRequest()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, request) = await StartAskingAsync(client);
        var answer = Answers(request, more: false) with
        {
            Items = [Answer($"{request.InformationRequestId}-more", "false"), Answer($"{request.InformationRequestId}-reason", "not-offered")],
        };

        var rejected = await PostAsync(client, AnswerUrl, answer, processId);
        Assert.NotEqual(StepExecutionStatus.Completed, Assert.Single(rejected.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Equal(request.ExchangeId, (await GetStateAsync(client, processId)).RequiredInformationRequest?.ExchangeId);
    }

    [Fact]
    public async Task ANewQuestionnaire_RejectsAnswersFromThePreviousRound()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, first) = await StartAskingAsync(client);
        var next = await PostAsync(client, AnswerUrl, Answers(first, more: true), processId);
        var second = Assert.IsType<InformationRequest>(next.RequiredInformationRequest);
        Assert.Equal(Questionnaires.SecondRound, second.InformationRequestId);
        Assert.NotEqual(first.ExchangeId, second.ExchangeId);

        var stale = await PostAsync(client, AnswerUrl, Answers(first, more: false), processId);
        Assert.NotEqual(StepExecutionStatus.Completed, Assert.Single(stale.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Equal(second.ExchangeId, (await GetStateAsync(client, processId)).RequiredInformationRequest?.ExchangeId);

        var current = await PostAsync(client, AnswerUrl, Answers(second, more: false), processId);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(current.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
    }

    [Fact]
    public async Task ADisabledConditionalQuestion_CannotBeAnswered()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, request) = await StartAskingAsync(client);
        var answers = Answers(request, more: false);
        var rejected = await PostAsync(client, AnswerUrl, answers with
        {
            Items = [.. answers.Items, Answer($"{request.InformationRequestId}-detail", "Not asked")],
        }, processId);

        Assert.NotEqual(StepExecutionStatus.Completed, Assert.Single(rejected.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Equal(request.ExchangeId, (await GetStateAsync(client, processId)).RequiredInformationRequest?.ExchangeId);
    }

    [Fact]
    public async Task ANewProcess_GetsItsOwnExchangeId()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var first = await StartAskingAsync(client);
        var second = await StartAskingAsync(client);

        Assert.NotEqual(first.ProcessId, second.ProcessId);
        Assert.NotEqual(first.Request.ExchangeId, second.Request.ExchangeId);
        Assert.Equal(first.Request.ExchangeId, (await GetStateAsync(client, first.ProcessId)).RequiredInformationRequest?.ExchangeId);
    }

    [Fact]
    public async Task AnotherStep_CannotBypassThePendingInformation()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, request) = await StartAskingAsync(client);

        var blocked = await PostAsync(client, AskUrl, new AskQuestionsStep(), processId);

        Assert.NotEqual(StepExecutionStatus.Completed, Assert.Single(blocked.Steps, step => step.StepName == nameof(AskQuestionsStep)).ExecutionStatus);
        Assert.Equal(request.ExchangeId, (await GetStateAsync(client, processId)).RequiredInformationRequest?.ExchangeId);
    }

    [Fact]
    public async Task AnswerWithoutAPendingRequest_CannotRunAgain()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var (processId, request) = await StartAskingAsync(client);
        var answer = Answers(request, more: false);
        var completed = await PostAsync(client, AnswerUrl, answer, processId);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(completed.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);

        var rejected = await PostAsync(client, AnswerUrl, answer, processId);

        Assert.NotEqual(StepExecutionStatus.Completed, Assert.Single(rejected.Steps, step => step.StepName == nameof(AnswerQuestionsStep)).ExecutionStatus);
        Assert.Null((await GetStateAsync(client, processId)).RequiredInformationRequest);
    }

    private static async Task<(Guid ProcessId, InformationRequest Request)> StartAskingAsync(HttpClient client)
    {
        var asked = await PostAsync(client, AskUrl, new AskQuestionsStep(), null);
        return (asked.ProcessId, Assert.IsType<InformationRequest>(asked.RequiredInformationRequest));
    }

    private static AnswerQuestionsStep Answers(InformationRequest request, bool more, string reason = "a") => new()
    {
        InformationRequestId = request.InformationRequestId,
        ExchangeId = request.ExchangeId,
        Items =
        [
            Answer($"{request.InformationRequestId}-more", more ? "true" : "false"),
            Answer($"{request.InformationRequestId}-reason", reason),
        ],
    };

    private static InformationResponseItem Answer(string id, string value) => new()
    {
        ItemId = id,
        Answers = [new InformationAnswer { Value = value }],
    };

    private static async Task<ProcessResult> PostAsync<TStep>(HttpClient client, string url, TStep step, Guid? processId)
        where TStep : IProcessStep
    {
        using var response = await SendAsync(client, url, step, processId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
    }

    private static async Task<HttpResponseMessage> SendAsync<TStep>(HttpClient client, string url, TStep step, Guid? processId)
        where TStep : IProcessStep
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new ExecuteStepRequest<TStep> { ProcessStep = step }, options: KaleidoJsonOptions.Options),
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
        }

        return await client.SendAsync(request, Cancel);
    }

    private static async Task<ProcessStateSnapshot> GetStateAsync(HttpClient client, Guid processId)
    {
        using var response = await client.GetAsync($"/catalog/1/processes/{processId:D}", Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProcessStateSnapshot>(await response.Content.ReadFromJsonAsync<ProcessStateSnapshot>(KaleidoJsonOptions.Options, Cancel));
    }

    private static HttpClient Client(WebApplication app)
    {
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reviewer"]));
        return client;
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddSingleton(new ProcessHttpServerFunctionalTests.StoredProcesses());
        builder.Services.AddSingleton(new ProcessHttpServerFunctionalTests.StoredIdempotency());
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(InformationRequestFunctionalTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(AskQuestionsStep).Namespace,
        })
            .AddEventing()
            .AddProcess()
            .AddContextStore<ProcessHttpServerFunctionalTests.TestProcessContextStore>()
            .AddIdempotencyStore<ProcessHttpServerFunctionalTests.TestIdempotencyStore>()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddProcessHttpServer();
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapProcessor();
        await app.StartAsync(Cancel);
        return app;
    }
}

[ProcessStep(DisplayName = "Ask questions", Description = "Asks the caller for additional information")]
[KaleidoAuthorization(Roles = "reviewer")]
public sealed record AskQuestionsStep : IProcessStep;

[ProcessStep(DisplayName = "Answer questions", Description = "Answers the pending questions")]
[AvailableAfter<AskQuestionsStep>]
[Repeatable]
[KaleidoAuthorization(Roles = "reviewer")]
public sealed record AnswerQuestionsStep : IInformationStep
{
    public string InformationRequestId { get; init; } = string.Empty;
    public string ExchangeId { get; init; } = string.Empty;
    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}

public sealed class AskQuestionsHandler : IProcessStepHandler<AskQuestionsStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(AskQuestionsStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.RequireInformation<AnswerQuestionsStep>(Questionnaires.Round(Questionnaires.FirstRound)));
}

public sealed class AnswerQuestionsHandler : IProcessStepHandler<AnswerQuestionsStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(AnswerQuestionsStep step, ProcessStepContext context, CancellationToken cancellationToken)
    {
        var more = bool.Parse(step.Items.Single(x => x.ItemId == $"{step.InformationRequestId}-more").Answers.Single().Value);
        var again = step.Items.Any(x => x.ItemId == $"{step.InformationRequestId}-reason" && x.Answers.Any(answer => answer.Value == "again"));
        return Task.FromResult(more
            ? ProcessStepHandlerResult.RequireInformation<AnswerQuestionsStep>(Questionnaires.Round(again ? step.InformationRequestId : Questionnaires.SecondRound))
            : ProcessStepHandlerResult.Success());
    }
}

public static class Questionnaires
{
    public const string FirstRound = "questions-first-round";
    public const string SecondRound = "questions-second-round";

    public static InformationRequest Round(string id) => new()
    {
        InformationRequestId = id,
        Title = "Questions",
        Items =
        [
            new InformationItem { Id = $"{id}-intro", Text = "Answer every question.", Type = InformationItemType.Display },
            new InformationItem { Id = $"{id}-more", Text = "Ask another round?", Type = InformationItemType.Boolean },
            new InformationItem
            {
                Id = $"{id}-reason",
                Text = "Why?",
                Type = InformationItemType.Choice,
                Options =
                [
                    new InformationOption { Value = "a", Display = "Reason A" },
                    new InformationOption { Value = "b", Display = "Reason B" },
                    new InformationOption { Value = "again", Display = "Ask these questions again" },
                ],
            },
            new InformationItem
            {
                Id = $"{id}-detail",
                Text = "Describe reason B.",
                Type = InformationItemType.Text,
                EnableWhen = [new InformationCondition { Question = $"{id}-reason", Operator = InformationConditionOperator.Equals, Answer = "b" }],
            },
        ],
    };
}
