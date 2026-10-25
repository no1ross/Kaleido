using System.Text;
using Kaleido.Http.Client;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class KaleidoToolInvokerTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly Guid ProcessId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private delegate bool TryGetProcessor(string service, out IKaleidoProcessorClient? client);

    private delegate bool TryGetQueryable(string service, out IKaleidoQueryableClient? client);

    private readonly Mock<IKaleidoProcessorClientFactory> processors = new();
    private readonly Mock<IKaleidoQueryableClientFactory> queryables = new();
    private readonly Dictionary<string, Mock<IKaleidoProcessorClient>> services = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clinic"] = new(),
        ["radiology"] = new()
    };
    private readonly Mock<IKaleidoQueryableClient> clinicQueries = new();
    private readonly Mock<IInformationRequestElicitor> elicitor = new();
    private readonly Mock<IKaleidoProcessForwarding> processForwarding = new();
    private readonly List<Guid?> forwardedProcessIds = [];
    private readonly Mock<McpServer> server = new();
    private readonly List<(string Service, string Step, JsonNode Body, Guid? ProcessId, string? IdempotencyKey)> posts = [];
    private readonly List<(string Source, string? View, QueryApiRequest<JsonObject> Request)> queries = [];

    private static readonly InformationRequest Questions = new()
    {
        InformationRequestId = "visit-intake",
        Items = [new InformationItem { Id = "urgent", Text = "Urgent?", Type = InformationItemType.Boolean }]
    };

    private static readonly KaleidoTool Search = new()
    {
        Definition = new Tool { Name = "clinic_patient" },
        Kind = KaleidoToolKind.Query,
        Service = "clinic",
        CapabilityName = "Patient",
        SourceName = "Patient",
        Url = "/clinic/queryable/patients/query"
    };

    private static readonly KaleidoTool SearchView = new()
    {
        Definition = new Tool { Name = "clinic_patient_summary" },
        Kind = KaleidoToolKind.Query,
        Service = "clinic",
        CapabilityName = "PatientSummary",
        SourceName = "Patient",
        ViewName = "PatientSummary",
        Url = "/clinic/queryable/patients/views/summary/query"
    };

    private static readonly KaleidoTool Start = new()
    {
        Definition = new Tool { Name = "clinic_start_visit" },
        Kind = KaleidoToolKind.Step,
        Service = "clinic",
        CapabilityName = "StartVisitStep",
        Url = "/clinic/processes/steps/StartVisitStep",
        IsInitial = true
    };

    private static readonly KaleidoTool Close = new()
    {
        Definition = new Tool { Name = "clinic_close_visit" },
        Kind = KaleidoToolKind.Step,
        Service = "clinic",
        CapabilityName = "CloseVisitStep",
        Url = "/clinic/processes/steps/CloseVisitStep"
    };

    private static readonly KaleidoTool Scan = new()
    {
        Definition = new Tool { Name = "radiology_capture_scan" },
        Kind = KaleidoToolKind.Step,
        Service = "radiology",
        CapabilityName = "CaptureScanStep",
        Url = "/radiology/processes/steps/CaptureScanStep"
    };

    private static readonly KaleidoTool Answer = new()
    {
        Definition = new Tool { Name = ToolCatalogBuilder.AnswerInformationRequestTool },
        Kind = KaleidoToolKind.AnswerInformationRequest
    };

    private static readonly KaleidoTool State = new()
    {
        Definition = new Tool { Name = ToolCatalogBuilder.GetProcessStateTool },
        Kind = KaleidoToolKind.GetProcessState
    };

    private static readonly ToolCatalog Catalog = new() { Tools = [Search, SearchView, Start, Close, Scan, Answer, State] };

    private static readonly ProcessRequiredStep CloseRequired = new() { Name = "CloseVisitStep", DisplayName = "Close visit", ExecuteUrl = Close.Url };

    private static readonly ProcessRequiredStep QuestionsRequired = new()
    {
        Name = "VisitQuestionsStep",
        IsInformationStep = true,
        ExecuteUrl = "/clinic/processes/steps/VisitQuestionsStep",
        InformationRequest = Questions
    };

    public KaleidoToolInvokerTests()
    {
        processors.Setup(x => x.TryGetClientForService(It.IsAny<string>(), out It.Ref<IKaleidoProcessorClient?>.IsAny))
            .Returns(new TryGetProcessor((string service, out IKaleidoProcessorClient? client) =>
            {
                client = services.TryGetValue(service, out var mock) ? mock.Object : null;
                return client is not null;
            }));
        queryables.Setup(x => x.TryGetClientForService(It.IsAny<string>(), out It.Ref<IKaleidoQueryableClient?>.IsAny))
            .Returns(new TryGetQueryable((string service, out IKaleidoQueryableClient? client) =>
            {
                client = service == "clinic" ? clinicQueries.Object : null;
                return client is not null;
            }));

        clinicQueries.Setup(x => x.QuerySourceAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .Callback((string source, QueryApiRequest<JsonObject> request, CancellationToken _) => queries.Add((source, null, request)))
            .ReturnsAsync(new QueryResult<JsonObject>(1, 0, 1, [new JsonObject { ["name"] = "Ava" }]));
        clinicQueries.Setup(x => x.QueryViewAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .Callback((string source, string view, QueryApiRequest<JsonObject> request, CancellationToken _) => queries.Add((source, view, request)))
            .ReturnsAsync(new QueryResult<JsonObject>(0, 0, 0, []));

        StepReturns(StepResponse());

        elicitor.Setup(x => x.ToElicitation(It.IsAny<InformationRequest>()))
            .Returns(new ElicitRequestParams { Message = "Visit intake", RequestedSchema = new ElicitRequestParams.RequestSchema() });
        elicitor.Setup(x => x.ToAnswers(It.IsAny<InformationRequest>(), It.IsAny<IReadOnlyDictionary<string, JsonElement>>()))
            .Returns([new InformationResponseItem { ItemId = "urgent", Answers = [new InformationAnswer { Value = "true" }] }]);

        server.Setup(x => x.ClientCapabilities).Returns(default(ClientCapabilities));

        processForwarding.Setup(x => x.ForwardProcess(It.IsAny<Guid?>()))
            .Callback((Guid? id) => forwardedProcessIds.Add(id))
            .Returns(Mock.Of<IDisposable>());
    }

    private KaleidoToolInvoker CreateSut(int maxElicitationRounds = 5, ILogger<KaleidoToolInvoker>? logger = null, bool requireConfirmation = false) =>
        new(
            processors.Object,
            queryables.Object,
            processForwarding.Object,
            elicitor.Object,
            new KaleidoMcpOptions
            {
                Instructions = "test",
                MaxElicitationRounds = maxElicitationRounds,
                RequireConfirmation = requireConfirmation
            },
            logger ?? NullLogger<KaleidoToolInvoker>.Instance);

    private KaleidoToolCall Call(object? arguments = null, string? requestState = null, IReadOnlyDictionary<string, InputResponse>? inputResponses = null) =>
        new()
        {
            Arguments = JsonSerializer.SerializeToElement(arguments ?? new { })
                .EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal),
            Server = server.Object,
            RequestState = requestState,
            InputResponses = inputResponses
        };

    private void ClientShowsForms() =>
        server.Setup(x => x.ClientCapabilities).Returns(new ClientCapabilities { Elicitation = new ElicitationCapability { Form = new() } });

    private static StepExecutionResponse<JsonElement> StepResponse(
        ProcessRequiredStep? required = null,
        string? target = null,
        StepExecutionOutcome outcome = StepExecutionOutcome.Completed,
        IReadOnlyCollection<ProcessStepSummary>? available = null) =>
        new()
        {
            ProcessId = ProcessId,
            StepName = "StartVisitStep",
            Outcome = outcome,
            RequiredStep = required,
            TargetProcessorName = target,
            AvailableSteps = available ?? [],
            BusinessMessages = [new ProcessMessage { Type = MessageType.Information, Code = "visit_opened", Message = "The visit is open." }],
            Result = JsonSerializer.SerializeToElement(new { visitId = "V-9" })
        };

    private static ProcessStateResponse StateResponse(ProcessRequiredStep? required, string? target = null, ProcessExecutionState state = ProcessExecutionState.AwaitingInformation) =>
        new()
        {
            ProcessId = ProcessId,
            State = state,
            RequiredStep = required,
            TargetProcessorName = target,
            Steps = [new ProcessStepHistory { StepName = "StartVisitStep", Status = StepExecutionStatus.Completed }]
        };

    private void StateIs(string service, ProcessStateResponse state) =>
        services[service].Setup(x => x.GetProcessStateAsync(ProcessId, It.IsAny<CancellationToken>())).ReturnsAsync(state);

    private void StepReturns(StepExecutionResponse<JsonElement> response)
    {
        foreach (var (service, client) in services)
        {
            client.Setup(x => x.ExecuteStepAsync(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback((string step, JsonElement body, Guid? processId, string? idempotencyKey, CancellationToken _) =>
                    posts.Add((service, step, JsonNode.Parse(body.GetRawText()) ?? new JsonObject(), processId, idempotencyKey)))
                .ReturnsAsync(response);
        }
    }

    private void StepThrows(Exception exception)
    {
        foreach (var client in services.Values)
        {
            client.Setup(x => x.ExecuteStepAsync(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }
    }

    private static JsonNode Body(ToolOutcome outcome) =>
        JsonNode.Parse((outcome.Result ?? throw new Xunit.Sdk.XunitException("No result.")).Content.OfType<TextContentBlock>().First().Text)!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    // ── Queries ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AQuery_RunsTheSourceQuery_WithASingleFilterAsACondition()
    {
        await CreateSut().InvokeAsync(
            Search,
            Call(new
            {
                searchText = "Ava",
                filters = new[] { new { field = "Id", @operator = "equals", values = new List<int> { 1 } } },
                sort = new[] { new { field = "Name", direction = "ascending" } },
                page = new { size = 10 },
                parameters = new { clinicId = "C1" }
            }),
            Catalog,
            Cancel);

        var (source, view, request) = Assert.Single(queries);
        Assert.Equal("Patient", source);
        Assert.Null(view);
        Assert.Equal("Ava", request.Query?.SearchText);
        Assert.Equal("Id", request.Query?.Filter?.Condition?.Field);
        Assert.Equal("equals", request.Query?.Filter?.Condition?.Operator);
        Assert.Equal("Name", Assert.Single(request.Query?.Sort ?? []).Field);
        Assert.Equal(10, request.Query?.Page?.Size);
        Assert.Equal("C1", request.Parameters?["clinicId"]?.GetValue<string>());
        Assert.Empty(posts);
    }

    [Fact]
    public async Task AQuery_WithSeveralFilters_JoinsThemWithAnd()
    {
        await CreateSut().InvokeAsync(
            Search,
            Call(new { filters = new object[] { new { field = "Id", @operator = "equals" }, new { field = "Name", @operator = "contains" } } }),
            Catalog,
            Cancel);

        var group = Assert.Single(queries).Request.Query?.Filter?.Group;
        Assert.Equal("and", group?.Operator);
        Assert.Equal(2, group?.Filters?.Count);
    }

    [Fact]
    public async Task AQuery_ReturnsTheSourcesResult()
    {
        var body = Body(await CreateSut().InvokeAsync(Search, Call(), Catalog, Cancel));

        Assert.Equal(1, body["totalCount"]?.GetValue<int>());
        Assert.Equal("Ava", body["results"]?[0]?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task AViewQuery_RunsTheViewOfItsSource()
    {
        await CreateSut().InvokeAsync(SearchView, Call(), Catalog, Cancel);

        var (source, view, request) = Assert.Single(queries);
        Assert.Equal("Patient", source);
        Assert.Equal("PatientSummary", view);
        Assert.Null(request.Parameters);
    }

    [Fact]
    public async Task AQuery_WithMalformedFilters_IsAnArgumentError_AndNothingIsSent()
    {
        var outcome = await CreateSut().InvokeAsync(Search, Call(new { filters = new[] { new { field = 1 } } }), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
        Assert.Empty(queries);
    }

    [Fact]
    public async Task AQueryTheServiceRefuses_IsAServiceError_WithItsStatusAndErrors()
    {
        clinicQueries.Setup(x => x.QuerySourceAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoHttpClientException(
                HttpClientErrorCodes.RequestFailed,
                "Query failed",
                HttpStatusCode.Forbidden,
                [new KaleidoError("forbidden", "No.")]));

        var outcome = await CreateSut().InvokeAsync(Search, Call(), Catalog, Cancel);

        Assert.True(outcome.Result?.IsError);
        Assert.Equal(McpErrorCodes.ServiceError, Body(outcome)["code"]?.GetValue<string>());
        Assert.Equal(403, Body(outcome)["details"]?["status"]?.GetValue<int>());
        Assert.Equal("forbidden", Body(outcome)["details"]?["errors"]?[0]?["code"]?.GetValue<string>());
    }

    // The service's own refusal reason is text for a person: also sent as a block for the user.
    // Kaleido's codes (qry_*, pro_*, argument_error) are the agent's to explain.
    [Fact]
    public async Task AServicesOwnRefusalReason_IsAlsoSentToTheUser_KaleidosCodesAreNot()
    {
        clinicQueries.Setup(x => x.QuerySourceAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoHttpClientException(
                HttpClientErrorCodes.RequestFailed,
                "Query failed",
                HttpStatusCode.BadRequest,
                [
                    new KaleidoError("MEMBER_PLAN_REQUIRED", "Capture the member first."),
                    new KaleidoError("qry_invalid_field", "Unknown field 'Foo'."),
                    new KaleidoError("argument_error", "The value at '$.x' is not valid."),
                ]));

        var outcome = await CreateSut().InvokeAsync(Search, Call(), Catalog, Cancel);

        Assert.Equal(["Capture the member first."], ForTheUser(outcome));
    }

    // The JSON summary is the agent's; each business message is also a block for the user, so a
    // client that honours the audience annotation shows it as written.
    [Fact]
    public async Task AStepsBusinessMessages_AreAlsoSentToTheUser_TheSummaryToTheAgent()
    {
        StepReturns(StepResponse() with
        {
            FrameworkMessages = [new ProcessMessage { Type = MessageType.Warning, Code = "pro_something", Message = "Check this." }],
        });

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);
        var summary = Assert.IsType<TextContentBlock>(outcome.Result?.Content[0]);

        Assert.Equal([Role.Assistant], summary.Annotations?.Audience);
        Assert.Equal(["The visit is open."], ForTheUser(outcome));
    }

    [Fact]
    public async Task AQueryResult_HasNothingForTheUser()
    {
        Assert.Empty(ForTheUser(await CreateSut().InvokeAsync(Search, Call(), Catalog, Cancel)));
    }

    private static string[] ForTheUser(ToolOutcome outcome) =>
        [.. (outcome.Result?.Content ?? [])
            .OfType<TextContentBlock>()
            .Where(x => x.Annotations?.Audience?.Contains(Role.User) == true)
            .Select(x => x.Text)];

    [Fact]
    public async Task AQueryTheServiceFails_TellsTheAgentWhatToDo_WithoutTheServicesMessage()
    {
        clinicQueries.Setup(x => x.QuerySourceAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoHttpClientException(
                HttpClientErrorCodes.RequestFailed,
                "clinic tried to call source 'PatientQueryContextSource', but the request failed with status code 500 (InternalServerError).",
                HttpStatusCode.InternalServerError));

        var message = Body(await CreateSut().InvokeAsync(Search, Call(), Catalog, Cancel))["message"]?.GetValue<string>();

        Assert.Contains("the service failed (500)", message, StringComparison.Ordinal);
        Assert.Contains("do not repeat the same call", message, StringComparison.Ordinal);
        Assert.DoesNotContain("PatientQueryContextSource", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AQuery_WithAProcessId_IsSentWithThatProcess_AndEchoesIt()
    {
        var body = Body(await CreateSut().InvokeAsync(Search, Call(new { processId = ProcessId, searchText = "Ava" }), Catalog, Cancel));

        Assert.Equal([ProcessId], forwardedProcessIds);
        Assert.Equal(ProcessId.ToString(), body["processId"]?.GetValue<string>());
        Assert.Null(Assert.Single(queries).Request.Parameters);
    }

    [Fact]
    public async Task AQuery_WithoutAProcessId_IsNotScoped()
    {
        var body = Body(await CreateSut().InvokeAsync(Search, Call(new { searchText = "Ava" }), Catalog, Cancel));

        Assert.Empty(forwardedProcessIds);
        Assert.Null(body["processId"]);
    }

    [Fact]
    public async Task AQuery_WithAnInvalidProcessId_IsAnArgumentError_AndNothingIsSent()
    {
        var outcome = await CreateSut().InvokeAsync(Search, Call(new { processId = "nope" }), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
        Assert.Empty(queries);
    }

    private static readonly string[] ProcessIdRequired = ["processId"];

    private static readonly KaleidoTool ScopedSearch = Search with
    {
        Definition = new Tool
        {
            Name = "clinic_scoped_patient",
            InputSchema = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new { processId = new { type = "string", description = "The visit this search is for." } },
                required = ProcessIdRequired
            })
        },
        ParametersTakeProcessId = true
    };

    [Fact]
    public async Task AQuery_ThatTakesTheProcessId_GetsItInItsParameters()
    {
        await CreateSut().InvokeAsync(ScopedSearch, Call(new { processId = ProcessId }), Catalog, Cancel);

        Assert.Equal(ProcessId.ToString(), Assert.Single(queries).Request.Parameters?["processId"]?.GetValue<string>());
        Assert.Equal([ProcessId], forwardedProcessIds);
    }

    [Fact]
    public async Task AQuery_AcceptsTheNestedProcessId_WhenTheTopLevelHasNone()
    {
        await CreateSut().InvokeAsync(ScopedSearch, Call(new { parameters = new { processId = ProcessId } }), Catalog, Cancel);

        Assert.Equal(ProcessId.ToString(), Assert.Single(queries).Request.Parameters?["processId"]?.GetValue<string>());
    }

    [Fact]
    public async Task AQuery_MissingARequiredArgument_SaysWhichAndWhy_AndNothingIsSent()
    {
        var outcome = await CreateSut().InvokeAsync(ScopedSearch, Call(new { searchText = "Ava" }), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
        Assert.Equal(
            "clinic_scoped_patient: processId is required: The visit this search is for. Nothing was sent.",
            Body(outcome)["message"]?.GetValue<string>());
        Assert.Empty(queries);
    }

    [Fact]
    public async Task AQuery_MissingARequiredNestedParameter_NamesItsPath()
    {
        var tool = Search with
        {
            Definition = new Tool
            {
                Name = "clinic_by_state",
                InputSchema = JsonSerializer.Deserialize<JsonElement>("""
                    {
                      "type": "object",
                      "properties": {
                        "parameters": {
                          "type": "object",
                          "properties": { "state": { "type": "string", "description": "Two-letter state code." } },
                          "required": ["state"]
                        }
                      },
                      "required": ["parameters"]
                    }
                    """)
            }
        };

        var outcome = await CreateSut().InvokeAsync(tool, Call(new { parameters = new { } }), Catalog, Cancel);

        Assert.Equal(
            "clinic_by_state: parameters.state is required: Two-letter state code. Nothing was sent.",
            Body(outcome)["message"]?.GetValue<string>());
        Assert.Empty(queries);
    }

    [Fact]
    public async Task AToolOfAServiceWithoutAClient_IsServiceUnavailable()
    {
        var orphan = Start with { Service = "billing" };

        var outcome = await CreateSut().InvokeAsync(orphan, Call(), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.ServiceUnavailable, Body(outcome)["code"]?.GetValue<string>());
        Assert.Contains("billing", Body(outcome)["message"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServiceThatCannotBeReached_IsServiceUnavailable()
    {
        StepThrows(new HttpRequestException("connection refused"));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.ServiceUnavailable, Body(outcome)["code"]?.GetValue<string>());
    }

    // ── Steps ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AStepThatNeedsAProcess_WithoutOne_IsAnArgumentError_AndNothingIsSent()
    {
        var outcome = await CreateSut().InvokeAsync(Close, Call(new { note = "x" }), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
        Assert.Empty(posts);
    }

    [Fact]
    public async Task AStep_WithAnInvalidProcessId_IsAnArgumentError()
    {
        var outcome = await CreateSut().InvokeAsync(Close, Call(new { processId = "nope" }), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task AStep_IsExecutedByName_OnItsService_ContinuingTheGivenProcess()
    {
        await CreateSut().InvokeAsync(Close, Call(new { processId = ProcessId, note = "done" }), Catalog, Cancel);

        var (service, step, body, processId, _) = Assert.Single(posts);
        Assert.Equal("clinic", service);
        Assert.Equal("CloseVisitStep", step);
        Assert.Equal(ProcessId, processId);
        Assert.Equal("done", body["note"]?.GetValue<string>());
        Assert.Null(body["processId"]);
    }

    [Fact]
    public async Task AStepResult_SummarizesTheOutcome_Messages_AndTheNextToolByName()
    {
        StepReturns(StepResponse(required: CloseRequired, available: [new ProcessStepSummary { Name = "CaptureScanStep", ExecuteUrl = Scan.Url }]));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(new { patientId = 1 }), Catalog, Cancel));

        Assert.Equal(ProcessId.ToString(), body["processId"]?.GetValue<string>());
        Assert.Equal("clinic", body["service"]?.GetValue<string>());
        Assert.Equal("completed", body["outcome"]?.GetValue<string>());
        Assert.Equal("V-9", body["result"]?["visitId"]?.GetValue<string>());
        Assert.Equal("The visit is open.", body["messages"]?[0]?["message"]?.GetValue<string>());
        Assert.Equal("clinic_close_visit", body["next"]?["required"]?["tool"]?.GetValue<string>());
        Assert.Equal("radiology_capture_scan", body["next"]?["available"]?[0]?["tool"]?.GetValue<string>());
    }

    [Fact]
    public async Task SeveralAvailableSteps_AreOfferedAsAChoice_WithWhatEachOneCloses()
    {
        // Capturing the scan closes closing the visit (CloseVisitStep is available until CaptureScanStep).
        // A 'with' copy of a used catalog must find the new tools, not the original's.
        Assert.NotNull(Catalog.FindByUrl(Close.Url));
        var catalog = Catalog with { Tools = [Search, SearchView, Start, Close with { ClosedBy = ["CaptureScanStep"] }, Scan, Answer, State] };
        StepReturns(StepResponse(available:
        [
            new ProcessStepSummary { Name = "CloseVisitStep", ExecuteUrl = Close.Url },
            new ProcessStepSummary { Name = "CaptureScanStep", ExecuteUrl = Scan.Url }
        ]));

        var next = Body(await CreateSut().InvokeAsync(Start, Call(), catalog, Cancel))["next"];

        var scan = next?["available"]?.AsArray().Single(x => x?["tool"]?.GetValue<string>() == "radiology_capture_scan");
        Assert.Equal("clinic_close_visit", scan?["closes"]?[0]?.GetValue<string>());
        Assert.Null(next?["available"]?.AsArray().Single(x => x?["tool"]?.GetValue<string>() == "clinic_close_visit")?["closes"]);
        Assert.Contains("let the user choose", next?["choice"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneAvailableStep_IsNotPresentedAsAChoice()
    {
        StepReturns(StepResponse(available: [new ProcessStepSummary { Name = "CaptureScanStep", ExecuteUrl = Scan.Url }]));

        var next = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel))["next"];

        Assert.Null(next?["choice"]);
    }

    [Fact]
    public async Task RejectedStep_SaysNothingChanged_AndKeepsTheNextSteps()
    {
        StepReturns(StepResponse(available: [new ProcessStepSummary { Name = "CaptureScanStep", ExecuteUrl = Scan.Url }]) with
        {
            Outcome = StepExecutionOutcome.Rejected
        });

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Equal("rejected", body["outcome"]?.GetValue<string>());
        Assert.Contains("nothing was changed", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Single(body["next"]?["available"]?.AsArray() ?? []);
    }

    [Fact]
    public async Task FailedStepWithNextSteps_SaysNothingWasRecorded()
    {
        StepReturns(StepResponse(available: [new ProcessStepSummary { Name = "CaptureScanStep", ExecuteUrl = Scan.Url }]) with
        {
            Outcome = StepExecutionOutcome.Failed
        });

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Contains("nothing was recorded", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    // The step response keeps business and framework messages apart, and so does the summary:
    // messages are shown to the user verbatim, diagnostics are explained by the agent.
    [Fact]
    public async Task BusinessMessages_AreMessages_FrameworkWarningsAreDiagnostics_Separately()
    {
        StepReturns(StepResponse() with
        {
            FrameworkMessages =
            [
                new ProcessMessage { Type = MessageType.Information, Code = "pro_repeatable_step", Message = "Step is repeatable." },
                new ProcessMessage { Type = MessageType.Warning, Code = "pro_something", Message = "Check this." }
            ]
        });

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Equal(["visit_opened"], Codes(body["messages"]));
        Assert.Equal(["pro_something"], Codes(body["diagnostics"]));
    }

    [Fact]
    public async Task WithoutFrameworkWarnings_ThereAreNoDiagnostics()
    {
        StepReturns(StepResponse() with
        {
            FrameworkMessages = [new ProcessMessage { Type = MessageType.Information, Code = "pro_repeatable_step", Message = "Step is repeatable." }]
        });

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Equal(["visit_opened"], Codes(body["messages"]));
        Assert.Null(body["diagnostics"]);
    }

    private static string?[] Codes(JsonNode? messages) =>
        [.. (messages?.AsArray() ?? []).Select(x => x?["code"]?.GetValue<string>())];

    [Fact]
    public async Task AStepResult_WithARequiredStepTheCallerCannotRun_SaysSo()
    {
        StepReturns(StepResponse(required: new ProcessRequiredStep { Name = "ApproveStep", ExecuteUrl = "/clinic/processes/steps/ApproveStep" }));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Null(body["next"]?["required"]?["tool"]);
        Assert.Contains("not allowed", body["next"]?["required"]?["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStepResult_WithNothingNext_SaysTheProcessIsComplete()
    {
        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Contains("complete", body["next"]?["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHandoff_IsFollowedToTheTargetService_SoNextNamesItsTools()
    {
        StepReturns(StepResponse(target: "radiology"));
        StateIs("radiology", StateResponse(new ProcessRequiredStep { Name = "CaptureScanStep", ExecuteUrl = Scan.Url }, state: ProcessExecutionState.Active));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Equal("radiology", body["handoff"]?.GetValue<string>());
        Assert.Equal("radiology", body["next"]?["service"]?.GetValue<string>());
        Assert.Equal("radiology_capture_scan", body["next"]?["required"]?["tool"]?.GetValue<string>());
    }

    [Fact]
    public async Task AStepThatFailedAtTheService_IsAServiceError()
    {
        StepThrows(new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            "Process step request for 'CloseVisitStep' failed",
            HttpStatusCode.Conflict,
            [new KaleidoError("pro_step_not_available", "Not now.")]));

        var outcome = await CreateSut().InvokeAsync(Close, Call(new { processId = ProcessId }), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.ServiceError, Body(outcome)["code"]?.GetValue<string>());
    }

    // Another call changed the process: the agent rereads the state instead of repeating the call.
    [Fact]
    public async Task AStepThatConflicted_IsAProcessConflict_ThatSaysToReadTheStateAgain()
    {
        StepThrows(new KaleidoHttpClientException(
            HttpClientErrorCodes.Conflict,
            "Process step request for 'CloseVisitStep' failed",
            HttpStatusCode.Conflict,
            [new KaleidoError("pro_process_conflict", "Changed by another request.")]));

        var outcome = await CreateSut().InvokeAsync(Close, Call(new { processId = ProcessId }), Catalog, Cancel);

        Assert.True(outcome.Result?.IsError);
        Assert.Equal(McpErrorCodes.ProcessConflict, Body(outcome)["code"]?.GetValue<string>());
        Assert.Contains("get_process_state", Body(outcome)["message"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    // ── Questions ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Questions_WithoutForms_AreReturnedForTheAgentToAskTheUser()
    {
        StepReturns(StepResponse(required: QuestionsRequired));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);
        var next = Body(outcome)["next"];

        Assert.Null(outcome.InputRequired);
        Assert.Equal(ToolCatalogBuilder.AnswerInformationRequestTool, next?["required"]?["tool"]?.GetValue<string>());
        Assert.Equal("visit-intake", next?["informationRequest"]?["informationRequestId"]?.GetValue<string>());
        Assert.Contains("Never answer for the user", next?["instruction"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Questions_WithProposedAnswers_AreReturned_WithAnInstructionToConfirmThem()
    {
        var proposing = QuestionsRequired with
        {
            InformationRequest = Questions with { Items = [Questions.Items[0] with { ProposedAnswers = [new InformationAnswer { Value = "true" }] }] }
        };
        StepReturns(StepResponse(required: proposing));

        var next = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel))["next"];

        Assert.Equal("true", next?["informationRequest"]?["items"]?[0]?["proposedAnswers"]?[0]?["value"]?.GetValue<string>());
        Assert.Contains("confirm or correct", next?["instruction"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("never submit a proposed answer the user has not confirmed", next?["instruction"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Questions_WithMrtrForms_ReturnInputRequired_WithTheFormAndAnOpaqueState()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: QuestionsRequired));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Null(outcome.Result);
        var inputRequired = Assert.IsType<InputRequiredResult>(outcome.InputRequired);
        Assert.Equal("Visit intake", Assert.Single(inputRequired.InputRequests!).Value.ElicitationParams?.Message);
        Assert.StartsWith("kaleido-mcp:", inputRequired.RequestState, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Questions_WhenTheStepDidNotComplete_AreNotPutToTheUser()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: QuestionsRequired, outcome: StepExecutionOutcome.Failed));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Null(outcome.InputRequired);
    }

    // ── Front door ──────────────────────────────────────────────────────────

    private static readonly KaleidoTool HiddenClose = Close with
    {
        Definition = new Tool
        {
            Name = "clinic_close_visit",
            Description = "Close visit: Closes the visit.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { patientId = new { type = "string" } } }),
            Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = true }
        },
        Listed = false,
        Lookups =
        [
            new ToolLookup
            {
                Group = "patient",
                Service = "clinic",
                Source = "Patient",
                QueryTool = "clinic_patient",
                Inputs = [("patientId", "PatientId")],
                Display = ["Name"]
            }
        ]
    };

    private static readonly ToolCatalog FrontDoorCatalog = new()
    {
        Tools = [Search, SearchView, Start, HiddenClose, Scan, Answer, State],
        IsFrontDoor = true
    };

    [Fact]
    public async Task FrontDoor_Next_DescribesAnUnlistedStep_WithItsInputs_AndItsSearchScopedToTheProcess()
    {
        StepReturns(StepResponse(required: CloseRequired));

        var required = Body(await CreateSut().InvokeAsync(Start, Call(new { patientId = 1 }), FrontDoorCatalog, Cancel))["next"]?["required"];

        Assert.Equal("clinic_close_visit", required?["tool"]?.GetValue<string>());
        Assert.Equal("Close visit: Closes the visit.", required?["description"]?.GetValue<string>());
        Assert.NotNull(required?["inputs"]?["properties"]?["patientId"]);
        Assert.True(required?["hints"]?["destructive"]?.GetValue<bool>());

        var search = required?["lookups"]?[0]?["search"];
        Assert.Equal("clinic_patient", search?["tool"]?.GetValue<string>());
        Assert.Equal(ProcessId.ToString(), search?["arguments"]?["processId"]?.GetValue<string>());
        Assert.Equal("PatientId", required?["lookups"]?[0]?["fills"]?["patientId"]?.GetValue<string>());
    }

    [Fact]
    public async Task FrontDoor_Next_SaysHowToRunUnlistedTools()
    {
        StepReturns(StepResponse(required: CloseRequired));

        var next = Body(await CreateSut().InvokeAsync(Start, Call(new { patientId = 1 }), FrontDoorCatalog, Cancel))["next"];

        Assert.Contains("call_tool", next?["howToRun"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListingEverything_Next_KeepsItsShape()
    {
        StepReturns(StepResponse(required: CloseRequired));

        var next = Body(await CreateSut().InvokeAsync(Start, Call(new { patientId = 1 }), Catalog, Cancel))["next"];

        Assert.Null(next?["required"]?["inputs"]);
        Assert.Null(next?["howToRun"]);
    }

    [Fact]
    public async Task SearchTools_FindsTheCallersToolsByWhatTheyDo()
    {
        var search = new KaleidoTool { Definition = new Tool { Name = ToolCatalogBuilder.SearchToolsTool }, Kind = KaleidoToolKind.SearchTools };

        var body = Body(await CreateSut().InvokeAsync(search, Call(new { text = "close visit" }), FrontDoorCatalog, Cancel));

        Assert.Equal("clinic_close_visit", body["tools"]?[0]?["name"]?.GetValue<string>());
        Assert.False(body["tools"]?[0]?["listed"]?.GetValue<bool>());
        Assert.Empty(queries);
        Assert.Empty(posts);
    }

    [Fact]
    public async Task SearchTools_WithoutText_IsAnArgumentError()
    {
        var search = new KaleidoTool { Definition = new Tool { Name = ToolCatalogBuilder.SearchToolsTool }, Kind = KaleidoToolKind.SearchTools };

        var outcome = await CreateSut().InvokeAsync(search, Call(), FrontDoorCatalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task DescribeTool_ReturnsATool_WithItsInputs_AndHowToRunIt()
    {
        var describe = new KaleidoTool { Definition = new Tool { Name = ToolCatalogBuilder.DescribeToolTool }, Kind = KaleidoToolKind.DescribeTool };

        var body = Body(await CreateSut().InvokeAsync(describe, Call(new { name = "clinic_close_visit" }), FrontDoorCatalog, Cancel));

        Assert.NotNull(body["inputs"]?["properties"]?["patientId"]);
        Assert.Contains("call_tool", body["howToRun"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(body["lookups"]?[0]?["search"]?["arguments"]);
    }

    [Fact]
    public async Task DescribeTool_ForAToolTheCallerDoesNotHave_IsAnUnknownToolResult()
    {
        var describe = new KaleidoTool { Definition = new Tool { Name = ToolCatalogBuilder.DescribeToolTool }, Kind = KaleidoToolKind.DescribeTool };

        var outcome = await CreateSut().InvokeAsync(describe, Call(new { name = "clinic_chart" }), FrontDoorCatalog, Cancel);

        Assert.Equal(McpErrorCodes.UnknownTool, Body(outcome)["code"]?.GetValue<string>());
    }

    // ── Guided lookups ──────────────────────────────────────────────────────

    private static readonly KaleidoTool PickPatient = new()
    {
        Definition = new Tool { Name = "clinic_pick_patient" },
        Kind = KaleidoToolKind.Step,
        Service = "clinic",
        CapabilityName = "PickPatientStep",
        Url = "/clinic/processes/steps/PickPatientStep",
        Lookups =
        [
            new ToolLookup
            {
                Group = "patient",
                Service = "clinic",
                Source = "Patient",
                QueryTool = "clinic_patient",
                QueryTitle = "Patient Search",
                Inputs = [("patientId", "PatientId"), ("enrollmentId", "EnrollmentId")],
                Display = ["Name"]
            }
        ]
    };

    private static readonly ToolCatalog LookupCatalog = Catalog with { Tools = [.. Catalog.Tools, PickPatient] };

    private void LookupForms()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);

        clinicQueries.Setup(x => x.QuerySourceAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .Callback((string source, QueryApiRequest<JsonObject> request, CancellationToken _) => queries.Add((source, null, request)))
            .ReturnsAsync(new QueryResult<JsonObject>(1, 0, 10, [new JsonObject { ["patientId"] = "P-1", ["enrollmentId"] = "E-1", ["name"] = "Ava Carter" }]));

        // The search form is answered with a search text, the pick form with the first result.
        elicitor.Setup(x => x.ToAnswers(It.IsAny<InformationRequest>(), It.IsAny<IReadOnlyDictionary<string, JsonElement>>()))
            .Returns((InformationRequest request, IReadOnlyDictionary<string, JsonElement> _) =>
                request.InformationRequestId.StartsWith("lookup-search", StringComparison.Ordinal)
                    ? [new InformationResponseItem { ItemId = "search", Answers = [new InformationAnswer { Value = "Ava" }] }]
                    : [new InformationResponseItem { ItemId = "result", Answers = [new InformationAnswer { Value = "0" }] }]);
    }

    private static Dictionary<string, InputResponse> LookupAnswered() =>
        new(StringComparer.Ordinal)
        {
            ["kaleido-lookup"] = new()
            {
                RawValue = JsonSerializer.SerializeToElement(
                    new ElicitResult { Action = "accept", Content = new Dictionary<string, JsonElement> { ["x"] = JsonSerializer.SerializeToElement("x") } },
                    McpJsonUtilities.DefaultOptions)
            }
        };

    [Fact]
    public async Task LookupStep_WithForms_AndMissingLookupInputs_AsksTheUserToSearch_WithoutRunningTheStep()
    {
        LookupForms();

        var outcome = await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel);

        var inputRequired = Assert.IsType<InputRequiredResult>(outcome.InputRequired);
        Assert.StartsWith("kaleido-mcp-lookup:", inputRequired.RequestState, StringComparison.Ordinal);
        Assert.Empty(posts);
        Assert.Empty(queries);
    }

    [Fact]
    public async Task TheSearchRetry_RunsTheLookupQuery_AndOffersTheResultsToPick()
    {
        LookupForms();
        var searchState = (await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel)).InputRequired?.RequestState;

        var outcome = await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        var (source, _, request) = Assert.Single(queries);
        Assert.Equal("Patient", source);
        Assert.Equal("Ava", request.Query?.SearchText);
        Assert.StartsWith("kaleido-mcp-lookup:", outcome.InputRequired?.RequestState, StringComparison.Ordinal);
        Assert.Empty(posts);
        Assert.Equal([ProcessId], forwardedProcessIds);
    }

    [Fact]
    public async Task ThePickRetry_FillsTheInputsFromTheChosenResult_AndRunsTheStep()
    {
        LookupForms();
        var searchState = (await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel)).InputRequired?.RequestState;
        var pickState = (await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel)).InputRequired?.RequestState;

        await CreateSut().InvokeAsync(PickPatient, Call(requestState: pickState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        var (_, step, body, processId, _) = Assert.Single(posts);
        Assert.Equal("PickPatientStep", step);
        Assert.Equal(ProcessId, processId);
        Assert.Equal("P-1", body["patientId"]?.GetValue<string>());
        Assert.Equal("E-1", body["enrollmentId"]?.GetValue<string>());
    }

    [Fact]
    public async Task LookupStep_WithoutForms_RunsTheStepAsCalled()
    {
        await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel);

        Assert.Single(posts);
        Assert.Empty(queries);
    }

    [Fact]
    public async Task LookupStep_WithItsInputsGiven_IsNotGuided()
    {
        LookupForms();

        await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId, patientId = "P-2", enrollmentId = "E-2" }), LookupCatalog, Cancel);

        Assert.Equal("P-2", Assert.Single(posts).Body["patientId"]?.GetValue<string>());
    }

    private async Task<string> MrtrStateAsync(ProcessRequiredStep? required = null)
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: required ?? QuestionsRequired));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);
        posts.Clear();

        return outcome.InputRequired?.RequestState ?? throw new Xunit.Sdk.XunitException("No state.");
    }

    private static Dictionary<string, InputResponse> Answered(string action) =>
        new(StringComparer.Ordinal)
        {
            ["kaleido-answers"] = new()
            {
                RawValue = JsonSerializer.SerializeToElement(
                    new ElicitResult
                    {
                        Action = action,
                        Content = action == "accept"
                            ? new Dictionary<string, JsonElement> { ["urgent"] = JsonSerializer.SerializeToElement(true) }
                            : null
                    },
                    McpJsonUtilities.DefaultOptions)
            }
        };

    [Fact]
    public async Task TheMrtrRetry_SubmitsTheUsersAnswers_ToThePendingRequest_AndCarriesOn()
    {
        var state = await MrtrStateAsync();
        StateIs("clinic", StateResponse(QuestionsRequired));
        StepReturns(StepResponse(required: CloseRequired));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(requestState: state, inputResponses: Answered("accept")), Catalog, Cancel));

        var (service, step, submitted, processId, _) = Assert.Single(posts);
        Assert.Equal("clinic", service);
        Assert.Equal(QuestionsRequired.Name, step);
        Assert.Equal(ProcessId, processId);
        Assert.Equal("visit-intake", submitted["informationRequestId"]?.GetValue<string>());
        Assert.Equal("true", submitted["items"]?[0]?["answers"]?[0]?["value"]?.GetValue<string>());
        Assert.Equal("visit-intake", body["answered"]?[0]?.GetValue<string>());
        Assert.Equal("clinic_close_visit", body["next"]?["required"]?["tool"]?.GetValue<string>());
    }

    [Fact]
    public async Task TheMrtrRetry_WhenTheUserDeclines_SubmitsNothing_AndSaysTheProcessIsWaiting()
    {
        var state = await MrtrStateAsync();
        StateIs("clinic", StateResponse(QuestionsRequired));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(requestState: state, inputResponses: Answered("decline")), Catalog, Cancel));

        Assert.Empty(posts);
        Assert.Contains("decline", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("awaitingInformation", body["state"]?.GetValue<string>());
    }

    [Fact]
    public async Task TheMrtrRetry_ForQuestionsTheProcessNoLongerAsks_SubmitsNothing()
    {
        var state = await MrtrStateAsync();
        StateIs("clinic", StateResponse(QuestionsRequired with { InformationRequest = Questions with { InformationRequestId = "newer" } }));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(requestState: state, inputResponses: Answered("accept")), Catalog, Cancel));

        Assert.Empty(posts);
        Assert.Contains("different questions", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreadableContinuationState_IsAnArgumentError()
    {
        var outcome = await CreateSut().InvokeAsync(Start, Call(requestState: "kaleido-mcp:%%%"), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
    }

    // ── answer_information_request / get_process_state ───────────────────────

    [Fact]
    public async Task AnswerInformationRequest_SubmitsTheAnswers_ToThePendingStep()
    {
        StateIs("clinic", StateResponse(QuestionsRequired));

        await CreateSut().InvokeAsync(
            Answer,
            Call(new { service = "clinic", processId = ProcessId, informationRequestId = "visit-intake", answers = new[] { new { itemId = "urgent", values = new List<string> { "true" } } } }),
            Catalog,
            Cancel);

        var (service, step, body, processId, _) = Assert.Single(posts);
        Assert.Equal("clinic", service);
        Assert.Equal(QuestionsRequired.Name, step);
        Assert.Equal(ProcessId, processId);
        Assert.Equal("urgent", body["items"]?[0]?["itemId"]?.GetValue<string>());
        Assert.Equal("true", body["items"]?[0]?["answers"]?[0]?["value"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnswerInformationRequest_ForADifferentRequest_IsRejected_WithTheCurrentQuestions()
    {
        StateIs("clinic", StateResponse(QuestionsRequired));

        var body = Body(await CreateSut().InvokeAsync(
            Answer,
            Call(new { service = "clinic", processId = ProcessId, informationRequestId = "old", answers = Array.Empty<object>() }),
            Catalog,
            Cancel));

        Assert.Empty(posts);
        Assert.Equal(McpErrorCodes.InvalidArguments, body["code"]?.GetValue<string>());
        Assert.Equal("visit-intake", body["details"]?["informationRequestId"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnswerInformationRequest_WhenNothingIsPending_SaysSo()
    {
        StateIs("clinic", StateResponse(CloseRequired, state: ProcessExecutionState.Active));

        var body = Body(await CreateSut().InvokeAsync(
            Answer,
            Call(new { service = "clinic", processId = ProcessId, informationRequestId = "visit-intake", answers = Array.Empty<object>() }),
            Catalog,
            Cancel));

        Assert.Equal(McpErrorCodes.NoPendingInformationRequest, body["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnswerInformationRequest_WithMissingArguments_IsAnArgumentError()
    {
        var body = Body(await CreateSut().InvokeAsync(Answer, Call(new { service = "clinic" }), Catalog, Cancel));

        Assert.Equal(McpErrorCodes.InvalidArguments, body["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetProcessState_SummarizesState_CompletedSteps_AndNext_FollowingAHandoff()
    {
        StateIs("clinic", StateResponse(null, target: "radiology", state: ProcessExecutionState.Active));
        StateIs("radiology", StateResponse(QuestionsRequired));

        var body = Body(await CreateSut().InvokeAsync(State, Call(new { service = "clinic", processId = ProcessId }), Catalog, Cancel));

        Assert.Equal("radiology", body["service"]?.GetValue<string>());
        Assert.Equal("awaitingInformation", body["state"]?.GetValue<string>());
        Assert.Equal("StartVisitStep", body["completedSteps"]?[0]?.GetValue<string>());
        Assert.Equal("visit-intake", body["next"]?["informationRequest"]?["informationRequestId"]?.GetValue<string>());
    }

    // ── Failures ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnUnexpectedFailure_IsAnInternalErrorResult_NotAnException()
    {
        StepThrows(new InvalidCastException("boom"));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.True(outcome.Result?.IsError);
        Assert.Equal(McpErrorCodes.InternalError, Body(outcome)["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task TheCallersCancellation_Propagates()
    {
        StepThrows(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));
    }

    [Fact]
    public async Task InvokeAsync_WithoutAToolACallOrACatalog_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().InvokeAsync(null!, Call(), Catalog, Cancel));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().InvokeAsync(Start, null!, Catalog, Cancel));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().InvokeAsync(Start, Call(), null!, Cancel));
    }

    [Fact]
    public async Task AToolKindWithoutAHandler_IsAnUnknownToolResult()
    {
        var callTool = new KaleidoTool { Definition = new Tool { Name = ToolCatalogBuilder.CallToolTool }, Kind = KaleidoToolKind.CallTool };

        var outcome = await CreateSut().InvokeAsync(callTool, Call(), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.UnknownTool, Body(outcome)["code"]?.GetValue<string>());
        Assert.Equal("Tool 'call_tool' has no handler.", Body(outcome)["message"]?.GetValue<string>());
    }

    // ── Logging ──────────────────────────────────────────────────────────────

    private sealed class RecordingLogger : ILogger<KaleidoToolInvoker>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Fact]
    public async Task AServiceRefusal_IsLoggedAtDebug_WithItsStatusAndCode()
    {
        var logger = new RecordingLogger();
        StepThrows(new KaleidoHttpClientException(HttpClientErrorCodes.RequestFailed, "Refused", HttpStatusCode.BadRequest));

        await CreateSut(logger: logger).InvokeAsync(Close, Call(new { processId = ProcessId }), Catalog, Cancel);
        await CreateSut(logger: logger).InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Equal(
            [
                (LogLevel.Debug, $"Kaleido MCP tool clinic_close_visit: the service answered 400 ({HttpClientErrorCodes.RequestFailed}).", (Exception?)null),
                (LogLevel.Debug, $"Kaleido MCP tool clinic_start_visit: the service answered 400 ({HttpClientErrorCodes.RequestFailed}).", null)
            ],
            logger.Entries);
    }

    [Fact]
    public async Task AnUnreachableService_IsLoggedAtWarning_WithTheFailure()
    {
        var logger = new RecordingLogger();
        var failure = new HttpRequestException("connection refused");
        StepThrows(failure);

        await CreateSut(logger: logger).InvokeAsync(Close, Call(new { processId = ProcessId }), Catalog, Cancel);
        var outcome = await CreateSut(logger: logger).InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Equal(
            [
                (LogLevel.Warning, "Kaleido MCP tool clinic_close_visit: the service could not be reached.", (Exception?)failure),
                (LogLevel.Warning, "Kaleido MCP tool clinic_start_visit: the service could not be reached.", failure)
            ],
            logger.Entries);
        Assert.Contains("could not be reached", Body(outcome)["message"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnexpectedFailure_IsLoggedAtError_WithTheFailure()
    {
        var logger = new RecordingLogger();
        var failure = new InvalidCastException("boom");
        StepThrows(failure);

        await CreateSut(logger: logger).InvokeAsync(Close, Call(new { processId = ProcessId }), Catalog, Cancel);
        await CreateSut(logger: logger).InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Equal(
            [
                (LogLevel.Error, "Kaleido MCP tool clinic_close_visit failed unexpectedly.", (Exception?)failure),
                (LogLevel.Error, "Kaleido MCP tool clinic_start_visit failed unexpectedly.", failure)
            ],
            logger.Entries);
    }

    [Fact]
    public async Task AServiceFailureWithReasons_ReportsTheServicesMessage_AndItsReasons()
    {
        StepThrows(new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            "The step failed",
            HttpStatusCode.ServiceUnavailable,
            [new KaleidoError("PLAN_LOCKED", "The plan is locked."), new KaleidoError("framework_error", "Internal."), new KaleidoError("OTHER", " ")]));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Equal("clinic_start_visit: The step failed", Body(outcome)["message"]?.GetValue<string>());
        Assert.Equal(503, Body(outcome)["details"]?["status"]?.GetValue<int>());
        Assert.Equal(["The plan is locked."], ForTheUser(outcome));
    }

    // ── Queries: shapes ──────────────────────────────────────────────────────

    [Fact]
    public async Task AQuery_IgnoresArgumentsOfTheWrongShape_AndSendsWhatIsLeft()
    {
        await CreateSut().InvokeAsync(
            Search,
            Call(new { searchText = "  ", filters = new { field = "Id" }, sort = (object?)null, page = (object?)null, parameters = "clinicId=C1" }),
            Catalog,
            Cancel);

        var request = Assert.Single(queries).Request;
        Assert.Null(request.Query?.SearchText);
        Assert.Null(request.Query?.Filter);
        Assert.Null(request.Query?.Sort);
        Assert.Null(request.Query?.Page);
        Assert.Null(request.Parameters);
    }

    [Fact]
    public async Task AQuery_WithAnEmptyFilterList_HasNoFilter()
    {
        await CreateSut().InvokeAsync(Search, Call(new { filters = Array.Empty<object>(), searchText = 7 }), Catalog, Cancel);

        var request = Assert.Single(queries).Request;
        Assert.Null(request.Query?.Filter);
        Assert.Null(request.Query?.SearchText);
    }

    [Fact]
    public async Task AQueryOfACapabilityWithoutASourceName_QueriesTheCapabilityByName()
    {
        var tool = Search with { SourceName = null, CapabilityName = "Clinic" };

        await CreateSut().InvokeAsync(tool, Call(), Catalog, Cancel);

        Assert.Equal("Clinic", Assert.Single(queries).Source);
    }

    [Fact]
    public async Task AQueryOfAServiceWithoutAQueryClient_IsServiceUnavailable()
    {
        var tool = Search with { Service = "radiology" };

        var outcome = await CreateSut().InvokeAsync(tool, Call(), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.ServiceUnavailable, Body(outcome)["code"]?.GetValue<string>());
        Assert.Contains("'radiology'", Body(outcome)["message"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AQuery_ThatTakesTheProcessId_WithoutOne_SendsItsParametersAsGiven()
    {
        var tool = ScopedSearch with { Definition = new Tool { Name = "clinic_scoped_patient" } };

        await CreateSut().InvokeAsync(tool, Call(new { parameters = new { clinicId = "C1" } }), Catalog, Cancel);

        var parameters = Assert.Single(queries).Request.Parameters;
        Assert.Equal("C1", parameters?["clinicId"]?.GetValue<string>());
        Assert.Null(parameters?["processId"]);
        Assert.Empty(forwardedProcessIds);
    }

    [Fact]
    public async Task AQuery_ThatTakesTheProcessId_AddsItToTheGivenParameters()
    {
        await CreateSut().InvokeAsync(ScopedSearch, Call(new { processId = ProcessId, parameters = new { clinicId = "C1" } }), Catalog, Cancel);

        var parameters = Assert.Single(queries).Request.Parameters;
        Assert.Equal("C1", parameters?["clinicId"]?.GetValue<string>());
        Assert.Equal(ProcessId.ToString(), parameters?["processId"]?.GetValue<string>());
    }

    [Fact]
    public async Task AQuery_WithANestedProcessIdThatIsNotText_IsNotScoped()
    {
        await CreateSut().InvokeAsync(Search, Call(new { parameters = new { processId = 5 } }), Catalog, Cancel);

        Assert.Empty(forwardedProcessIds);
        Assert.Equal(5, Assert.Single(queries).Request.Parameters?["processId"]?.GetValue<int>());
    }

    // ── Queries: required arguments ──────────────────────────────────────────

    private static readonly KaleidoTool ByArea = Search with
    {
        Definition = new Tool
        {
            Name = "clinic_by_area",
            InputSchema = JsonSerializer.Deserialize<JsonElement>("""
                {
                  "type": "object",
                  "properties": {
                    "processId": { "type": "string" },
                    "area": {
                      "type": "object",
                      "properties": { "state": { "type": "string", "description": 5 } },
                      "required": ["state", "zip", 3]
                    },
                    "name": { "type": "string" }
                  },
                  "required": ["ghost", "processId", "area", "name"]
                }
                """)
        }
    };

    [Theory]
    [InlineData("""{"area":{"state":" "}}""", "clinic_by_area: area.state is required. Nothing was sent.")]
    [InlineData("""{"area":{"state":"CA","zip":null}}""", "clinic_by_area: area.zip is required. Nothing was sent.")]
    [InlineData("""{"area":{"state":"CA","zip":"94110"}}""", "clinic_by_area: name is required. Nothing was sent.")]
    [InlineData("""{"area":"CA","name":" "}""", "clinic_by_area: name is required. Nothing was sent.")]
    public async Task AQuery_MissingARequiredValue_NamesIt_AndNothingIsSent(string given, string expected)
    {
        var arguments = JsonNode.Parse(given)!.AsObject();
        arguments["processId"] = ProcessId.ToString();

        var outcome = await CreateSut().InvokeAsync(ByArea, Call(arguments), Catalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
        Assert.Equal(expected, Body(outcome)["message"]?.GetValue<string>());
        Assert.Empty(queries);
    }

    [Fact]
    public async Task AQuery_WithEveryRequiredValue_IsSent()
    {
        await CreateSut().InvokeAsync(ByArea, Call(new { processId = ProcessId, area = new { state = "CA", zip = "94110" }, name = "Ava" }), Catalog, Cancel);

        Assert.Single(queries);
    }

    [Theory]
    [InlineData("""{"type":"object","properties":[]}""")]
    [InlineData("""{"type":"object"}""")]
    [InlineData("""{"type":"object","properties":{"name":{}},"required":"name"}""")]
    public async Task AQuery_WithoutUsableRequiredInputs_IsSent(string schema)
    {
        var tool = Search with { Definition = new Tool { Name = "clinic_loose", InputSchema = JsonSerializer.Deserialize<JsonElement>(schema) } };

        await CreateSut().InvokeAsync(tool, Call(), Catalog, Cancel);

        Assert.Single(queries);
    }

    // ── Steps: arguments ─────────────────────────────────────────────────────

    [Fact]
    public async Task AnInitialStep_WithAProcessId_ContinuesThatProcess()
    {
        await CreateSut().InvokeAsync(Start, Call(new { processId = ProcessId }), Catalog, Cancel);

        Assert.Equal(ProcessId, Assert.Single(posts).ProcessId);
    }

    // ── Steps: one idempotency key per logical call (#440) ─────────────────

    // The agent calling the same step again with the same inputs for the same process (after a timeout,
    // a lost answer) is the same call: same key, same body, so the service replays its stored answer.
    [Fact]
    public async Task AStepContinuingAProcess_CalledAgainWithTheSameInputs_SendsTheSameKeyAndBody()
    {
        var sut = CreateSut();

        await sut.InvokeAsync(Close, Call(new { processId = ProcessId, reason = "done", details = new { code = 7, note = "x" } }), Catalog, Cancel);
        await sut.InvokeAsync(Close, Call(new { details = new { note = "x", code = 7 }, reason = "done", processId = ProcessId }), Catalog, Cancel);

        Assert.Equal(2, posts.Count);
        Assert.StartsWith("mcp-", posts[0].IdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(44, posts[0].IdempotencyKey!.Length);
        Assert.Equal(posts[0].IdempotencyKey, posts[1].IdempotencyKey);
        Assert.Equal(posts[0].Body.ToJsonString(), posts[1].Body.ToJsonString());
    }

    private static readonly int[] ThreeFour = [3, 4];

    // Arrays keep their order (it is meaningful), the objects in them are canonical too, and nulls
    // stay nulls.
    [Fact]
    public async Task AStepBody_IsCanonical_InsideArrays_AndKeepsNulls()
    {
        var sut = CreateSut();

        await sut.InvokeAsync(Close, Call(new { processId = ProcessId, lines = new object?[] { new { b = 2, a = 1 }, null, ThreeFour }, note = (string?)null }), Catalog, Cancel);
        await sut.InvokeAsync(Close, Call(new { note = (string?)null, lines = new object?[] { new { a = 1, b = 2 }, null, ThreeFour }, processId = ProcessId }), Catalog, Cancel);

        Assert.Equal("""{"lines":[{"a":1,"b":2},null,[3,4]],"note":null}""", posts[0].Body.ToJsonString());
        Assert.Equal(posts[0].Body.ToJsonString(), posts[1].Body.ToJsonString());
        Assert.Equal(posts[0].IdempotencyKey, posts[1].IdempotencyKey);
    }

    [Theory]
    [InlineData("other reason", "86a1ed5b-0b4a-4f6e-9f3c-4cb2f5b1a001")]
    [InlineData("done", "1b4f1c11-3a0e-4c55-8f0f-9a3c2d6e7b02")]
    public async Task AStepWithOtherInputsOrAnotherProcess_GetsAnotherKey(string reason, string otherProcess)
    {
        var sut = CreateSut();

        await sut.InvokeAsync(Close, Call(new { processId = ProcessId, reason = "done" }), Catalog, Cancel);
        await sut.InvokeAsync(Close, Call(new { processId = reason == "done" ? Guid.Parse(otherProcess) : ProcessId, reason }), Catalog, Cancel);

        Assert.NotEqual(posts[0].IdempotencyKey, posts[1].IdempotencyKey);
    }

    // A repeatable step called twice is two operations; two starts without a process id are two
    // processes: neither is deduplicated (the client gives every request its own key).
    [Fact]
    public async Task ARepeatableStep_OrANewProcess_SendsNoKeyOfItsOwn()
    {
        var sut = CreateSut();

        await sut.InvokeAsync(Close with { Repeatable = true }, Call(new { processId = ProcessId, reason = "done" }), Catalog, Cancel);
        await sut.InvokeAsync(Start, Call(new { reason = "new" }), Catalog, Cancel);

        Assert.All(posts, x => Assert.Null(x.IdempotencyKey));
    }

    [Fact]
    public async Task AStepResult_WithoutAResult_OrMessages_HasNeither()
    {
        StepReturns(StepResponse() with { Result = default, BusinessMessages = [] });

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);
        var body = Body(outcome);

        Assert.Null(body["result"]);
        Assert.Null(body["messages"]);
        Assert.Empty(ForTheUser(outcome));
    }

    [Fact]
    public async Task AStepResult_ThatIsNull_HasNoResult()
    {
        StepReturns(StepResponse() with { Result = JsonSerializer.SerializeToElement<object?>(null) });

        Assert.Null(Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel))["result"]);
    }

    [Fact]
    public async Task AFailedStep_WithNothingNext_HasNoNote()
    {
        StepReturns(StepResponse(outcome: StepExecutionOutcome.Failed));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Equal("failed", body["outcome"]?.GetValue<string>());
        Assert.Null(body["note"]);
    }

    [Fact]
    public async Task AFailedStep_WithARequiredStep_SaysNothingWasRecorded()
    {
        StepReturns(StepResponse(required: CloseRequired, outcome: StepExecutionOutcome.Failed));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Contains("nothing was recorded", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAvailableStep_IsOfferedByItsDisplayName_AndUnknownUrlsAreLeftOut()
    {
        StepReturns(StepResponse(available:
        [
            new ProcessStepSummary { Name = "CaptureScanStep", DisplayName = "Capture scan", ExecuteUrl = Scan.Url },
            new ProcessStepSummary { Name = "SecretStep", ExecuteUrl = "/clinic/processes/steps/SecretStep" }
        ]));

        var available = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel))["next"]?["available"]?.AsArray();

        var offered = Assert.Single(available ?? []);
        Assert.Equal("radiology_capture_scan", offered?["tool"]?.GetValue<string>());
        Assert.Equal("Capture scan", offered?["displayName"]?.GetValue<string>());
    }

    [Fact]
    public async Task AHandoff_ToATargetWhoseStateCannotBeRead_HasNothingNext()
    {
        StepReturns(StepResponse(required: CloseRequired, target: "radiology"));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Equal("radiology", body["handoff"]?.GetValue<string>());
        Assert.Null(body["next"]?["required"]);
        Assert.Contains("complete", body["next"]?["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    // ── Confirmation (#439) ──────────────────────────────────────────────────

    private static readonly KaleidoTool Discharge = new()
    {
        Definition = new Tool
        {
            Name = "clinic_discharge",
            Title = "Discharge patient",
            Annotations = new ToolAnnotations { DestructiveHint = true }
        },
        Kind = KaleidoToolKind.Step,
        Service = "clinic",
        CapabilityName = "DischargeStep",
        Url = "/clinic/processes/steps/DischargeStep",
        RequiresConfirmation = true
    };

    private static readonly ToolCatalog ConfirmCatalog = new() { Tools = [Start, Close, Discharge, State] };

    private static readonly object DischargeArguments = new { processId = ProcessId, reason = "recovered" };

    private static ElicitResult Confirmation(string action, bool? confirm) =>
        new()
        {
            Action = action,
            Content = confirm is { } value ? new Dictionary<string, JsonElement> { ["confirm"] = JsonSerializer.SerializeToElement(value) } : null
        };

    private static Dictionary<string, InputResponse> Confirmed(ElicitResult answer) =>
        new(StringComparer.Ordinal)
        {
            ["kaleido-confirm"] = new() { RawValue = JsonSerializer.SerializeToElement(answer, McpJsonUtilities.DefaultOptions) }
        };

    private async Task<string> ConfirmationStateAsync()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);

        var outcome = await CreateSut(requireConfirmation: true).InvokeAsync(Discharge, Call(DischargeArguments), ConfirmCatalog, Cancel);

        return outcome.InputRequired?.RequestState ?? throw new Xunit.Sdk.XunitException("No confirmation state.");
    }

    [Fact]
    public async Task Confirmation_IsOff_ByDefault_TheStepRuns()
    {
        await CreateSut().InvokeAsync(Discharge, Call(DischargeArguments), ConfirmCatalog, Cancel);

        Assert.Single(posts);
    }

    [Fact]
    public async Task Confirmation_OnlyForStepsThatRequireIt()
    {
        await CreateSut(requireConfirmation: true).InvokeAsync(Close, Call(new { processId = ProcessId }), ConfirmCatalog, Cancel);

        Assert.Single(posts);
    }

    [Fact]
    public async Task Confirmation_WithoutForms_IsRefused_AndNothingRuns()
    {
        var outcome = await CreateSut(requireConfirmation: true).InvokeAsync(Discharge, Call(DischargeArguments), ConfirmCatalog, Cancel);

        Assert.True(outcome.Result?.IsError);
        Assert.Equal(McpErrorCodes.ConfirmationRequired, Body(outcome)["code"]?.GetValue<string>());
        Assert.Empty(posts);
    }

    // The agent saying the user agreed, or adding a confirmation argument, is not a confirmation.
    [Fact]
    public async Task Confirmation_AnAgentsClaim_IsNotAConfirmation()
    {
        var outcome = await CreateSut(requireConfirmation: true).InvokeAsync(
            Discharge, Call(new { processId = ProcessId, reason = "recovered", confirm = true, userConfirmed = true }), ConfirmCatalog, Cancel);

        Assert.Equal(McpErrorCodes.ConfirmationRequired, Body(outcome)["code"]?.GetValue<string>());
        Assert.Empty(posts);
    }

    [Fact]
    public async Task Confirmation_InASession_Accepted_RunsTheStep_AfterAFormNamingItAndItsInputs()
    {
        UserAnswersInASession(Confirmation("accept", confirm: true));

        await CreateSut(requireConfirmation: true).InvokeAsync(Discharge, Call(DischargeArguments), ConfirmCatalog, Cancel);

        var form = Assert.Single(sessionForms);
        Assert.Contains("Discharge patient", form.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be undone", form.Message, StringComparison.Ordinal);
        Assert.Contains("reason: \"recovered\"", form.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("processId", form.Message, StringComparison.Ordinal);
        Assert.Equal(["confirm"], form.RequestedSchema?.Required);
        var posted = Assert.Single(posts);
        Assert.Equal("recovered", posted.Body["reason"]?.GetValue<string>());
        Assert.Equal(ProcessId, posted.ProcessId);
    }

    [Theory]
    [InlineData("decline", null)]
    [InlineData("cancel", null)]
    [InlineData("accept", false)]
    [InlineData("accept", null)]
    public async Task Confirmation_InASession_NotAccepted_RunsNothing(string action, bool? confirm)
    {
        UserAnswersInASession(Confirmation(action, confirm));

        var body = Body(await CreateSut(requireConfirmation: true).InvokeAsync(Discharge, Call(DischargeArguments), ConfirmCatalog, Cancel));

        Assert.Equal("notRun", body["outcome"]?.GetValue<string>());
        Assert.Contains($"({action})", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(posts);
    }

    [Fact]
    public async Task Confirmation_WithMrtr_ReturnsTheForm_AndRunsNothingYet()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);

        var outcome = await CreateSut(requireConfirmation: true).InvokeAsync(Discharge, Call(DischargeArguments), ConfirmCatalog, Cancel);

        var request = Assert.Single(outcome.InputRequired?.InputRequests ?? new Dictionary<string, InputRequest>());
        Assert.Equal("kaleido-confirm", request.Key);
        Assert.StartsWith("kaleido-mcp-confirm:", outcome.InputRequired?.RequestState, StringComparison.Ordinal);
        Assert.Empty(posts);
    }

    [Fact]
    public async Task Confirmation_WithMrtr_Accepted_RunsTheConfirmedCall()
    {
        var state = await ConfirmationStateAsync();

        await CreateSut(requireConfirmation: true).InvokeAsync(
            Discharge, Call(requestState: state, inputResponses: Confirmed(Confirmation("accept", confirm: true))), ConfirmCatalog, Cancel);

        var posted = Assert.Single(posts);
        Assert.Equal("DischargeStep", posted.Step);
        Assert.Equal("recovered", posted.Body["reason"]?.GetValue<string>());
        Assert.Equal(ProcessId, posted.ProcessId);
    }

    [Theory]
    [InlineData("decline", null)]
    [InlineData("accept", false)]
    public async Task Confirmation_WithMrtr_NotAccepted_RunsNothing(string action, bool? confirm)
    {
        var state = await ConfirmationStateAsync();

        var body = Body(await CreateSut(requireConfirmation: true).InvokeAsync(
            Discharge, Call(requestState: state, inputResponses: Confirmed(Confirmation(action, confirm))), ConfirmCatalog, Cancel));

        Assert.Equal("notRun", body["outcome"]?.GetValue<string>());
        Assert.Empty(posts);
    }

    // A retry with the state but no answer (an agent replaying the round) runs nothing.
    [Fact]
    public async Task Confirmation_WithMrtr_ARetryWithoutAnAnswer_RunsNothing()
    {
        var state = await ConfirmationStateAsync();

        var body = Body(await CreateSut(requireConfirmation: true).InvokeAsync(Discharge, Call(requestState: state), ConfirmCatalog, Cancel));

        Assert.Equal("notRun", body["outcome"]?.GetValue<string>());
        Assert.Contains("no answer", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(posts);
    }

    [Theory]
    [InlineData("kaleido-mcp-confirm:not base64!")]
    [InlineData("kaleido-mcp-confirm:eyJ0b29sIjoiY2xpbmljX3Vua25vd24ifQ==")]
    public async Task Confirmation_WithMrtr_AnUnreadableState_IsRefused(string state)
    {
        var outcome = await CreateSut(requireConfirmation: true).InvokeAsync(
            Discharge, Call(requestState: state, inputResponses: Confirmed(Confirmation("accept", confirm: true))), ConfirmCatalog, Cancel);

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(outcome)["code"]?.GetValue<string>());
        Assert.Empty(posts);
    }

    // ── Questions: session elicitation ───────────────────────────────────────

    private readonly Queue<ElicitResult> sessionAnswers = new();
    private readonly List<ElicitRequestParams> sessionForms = [];

    private static ElicitResult Accepted(bool withContent = true) =>
        new() { Action = "accept", Content = withContent ? new Dictionary<string, JsonElement> { ["x"] = JsonSerializer.SerializeToElement("x") } : null };

    private static ElicitResult Declined() => new() { Action = "decline" };

    private void UserAnswersInASession(params ElicitResult[] answers)
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(false);

        foreach (var answer in answers)
        {
            sessionAnswers.Enqueue(answer);
        }

        server.Setup(x => x.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()))
            .Callback((JsonRpcRequest request, CancellationToken _) =>
                sessionForms.Add(JsonSerializer.Deserialize<ElicitRequestParams>(request.Params, McpJsonUtilities.DefaultOptions) ?? throw new Xunit.Sdk.XunitException("No form.")))
            .ReturnsAsync((JsonRpcRequest request, CancellationToken _) => new JsonRpcResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToNode(sessionAnswers.Dequeue(), McpJsonUtilities.DefaultOptions)
            });
    }

    private void StepsReturn(params StepExecutionResponse<JsonElement>[] responses)
    {
        var queue = new Queue<StepExecutionResponse<JsonElement>>(responses);

        foreach (var (service, client) in services)
        {
            client.Setup(x => x.ExecuteStepAsync(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback((string step, JsonElement body, Guid? processId, string? idempotencyKey, CancellationToken _) =>
                    posts.Add((service, step, JsonNode.Parse(body.GetRawText()) ?? new JsonObject(), processId, idempotencyKey)))
                .ReturnsAsync(queue.Dequeue);
        }
    }

    private static readonly InformationRequest FollowUpQuestions = Questions with { InformationRequestId = "follow-up" };

    private static readonly ProcessRequiredStep FollowUpRequired = QuestionsRequired with { InformationRequest = FollowUpQuestions };

    [Fact]
    public async Task Questions_InASession_AreAskedDuringTheCall_AndTheAnswersSubmitted()
    {
        UserAnswersInASession(Accepted());
        StepsReturn(StepResponse(required: QuestionsRequired), StepResponse(required: CloseRequired));

        var outcome = await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);
        var body = Body(outcome);

        Assert.Null(outcome.InputRequired);
        Assert.Equal("Visit intake", Assert.Single(sessionForms).Message);
        Assert.Equal(2, posts.Count);
        Assert.Equal(QuestionsRequired.Name, posts[1].Step);
        Assert.Equal("visit-intake", posts[1].Body["informationRequestId"]?.GetValue<string>());
        Assert.Equal("true", posts[1].Body["items"]?[0]?["answers"]?[0]?["value"]?.GetValue<string>());
        Assert.Equal(["visit-intake"], body["answered"]?.AsArray().Select(x => x?.GetValue<string>()));
        Assert.Equal("clinic_close_visit", body["next"]?["required"]?["tool"]?.GetValue<string>());
        Assert.Null(body["note"]);

        // #440: the start of a new process has no key of its own; the answers to one request do.
        Assert.Null(posts[0].IdempotencyKey);
        Assert.StartsWith("mcp-", posts[1].IdempotencyKey, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Questions_InASession_TheUserDoesNotAnswer_NothingIsSubmitted(bool declined)
    {
        UserAnswersInASession(declined ? Declined() : Accepted(withContent: false));
        StepReturns(StepResponse(required: QuestionsRequired));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Single(posts);
        Assert.Equal(
            $"The user did not answer the questions ({(declined ? "decline" : "accept")}). Nothing was submitted; the process is still waiting for them.",
            body["note"]?.GetValue<string>());
        Assert.Equal("visit-intake", body["next"]?["informationRequest"]?["informationRequestId"]?.GetValue<string>());
    }

    [Fact]
    public async Task Questions_BeyondTheMaximumRounds_AreLeftForTheAgent_SayingMoreFollowed()
    {
        UserAnswersInASession(Accepted());
        StepsReturn(StepResponse(required: QuestionsRequired), StepResponse(required: FollowUpRequired));

        var body = Body(await CreateSut(maxElicitationRounds: 1).InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Single(sessionForms);
        Assert.Contains("more rounds of questions", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("More questions followed. ", body["next"]?["instruction"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("follow-up", body["next"]?["informationRequest"]?["informationRequestId"]?.GetValue<string>());
    }

    [Fact]
    public async Task Questions_WithoutAnyRoundsAllowed_AreLeftForTheAgent()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: QuestionsRequired));

        var outcome = await CreateSut(maxElicitationRounds: 0).InvokeAsync(Start, Call(), Catalog, Cancel);
        var body = Body(outcome);

        Assert.Null(outcome.InputRequired);
        Assert.Contains("more rounds of questions", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("Ask the user", body["next"]?["instruction"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Questions_ForAClientThatAnnouncesFormsPerRequest_UseMrtr()
    {
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: QuestionsRequired));
        var call = Call() with { Meta = new JsonObject { ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject { ["elicitation"] = new JsonObject() } } };

        var outcome = await CreateSut().InvokeAsync(Start, call, Catalog, Cancel);

        Assert.StartsWith("kaleido-mcp:", outcome.InputRequired?.RequestState, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Questions_ForAClientWithoutElicitationInItsRequestCapabilities_AreReturned()
    {
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: QuestionsRequired));
        var call = Call() with { Meta = new JsonObject { ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject { ["sampling"] = new JsonObject() } } };

        var outcome = await CreateSut().InvokeAsync(Start, call, Catalog, Cancel);

        Assert.Null(outcome.InputRequired);
        Assert.NotNull(Body(outcome)["next"]?["informationRequest"]);
    }

    // ── Questions: MRTR rounds ───────────────────────────────────────────────

    private static string Decode(string state, string prefix) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(state[prefix.Length..]));

    private static string Recode(string state, string prefix, Action<JsonObject> change)
    {
        var json = JsonNode.Parse(Decode(state, prefix))?.AsObject() ?? throw new Xunit.Sdk.XunitException("No state.");
        change(json);
        return prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToJsonString()));
    }

    [Fact]
    public async Task TheMrtrRetry_WhenMoreQuestionsFollow_AsksAgain_RememberingWhatWasAnswered()
    {
        var state = await MrtrStateAsync();
        StateIs("clinic", StateResponse(QuestionsRequired));
        StepReturns(StepResponse(required: FollowUpRequired));

        var outcome = await CreateSut().InvokeAsync(Start, Call(requestState: state, inputResponses: Answered("accept")), Catalog, Cancel);

        var next = outcome.InputRequired?.RequestState ?? throw new Xunit.Sdk.XunitException("No next round.");
        var decoded = JsonNode.Parse(Decode(next, "kaleido-mcp:"));
        Assert.Equal("follow-up", decoded?["informationRequestId"]?.GetValue<string>());
        Assert.Equal(1, decoded?["round"]?.GetValue<int>());
        Assert.Equal(["visit-intake"], decoded?["answered"]?.AsArray().Select(x => x?.GetValue<string>()));

        posts.Clear();
        StateIs("clinic", StateResponse(FollowUpRequired));
        StepReturns(StepResponse(required: CloseRequired));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(requestState: next, inputResponses: Answered("accept")), Catalog, Cancel));

        Assert.Equal("follow-up", Assert.Single(posts).Body["informationRequestId"]?.GetValue<string>());
        Assert.Equal(["visit-intake", "follow-up"], body["answered"]?.AsArray().Select(x => x?.GetValue<string>()));
    }

    // ── Output descriptions (#213) ───────────────────────────────────────────
    // The agent receives what the returned fields mean, for the fields the result contains.

    private static readonly Dictionary<string, string> PatientFields =
        new(StringComparer.OrdinalIgnoreCase) { ["Name"] = "The patient's full name.", ["Phone"] = "Not in the result." };

    [Fact]
    public async Task AQueryResult_CarriesTheDescriptionsOfItsFields()
    {
        var body = Body(await CreateSut().InvokeAsync(Search with { OutputDescriptions = PatientFields }, Call(), Catalog, Cancel));

        var described = body["fieldDescriptions"]?.AsObject() ?? throw new Xunit.Sdk.XunitException("No field descriptions.");
        Assert.Equal("The patient's full name.", described["name"]?.GetValue<string>());
        Assert.Single(described);
    }

    [Fact]
    public async Task AQueryResult_WithoutDescribedFields_HasNoFieldDescriptions()
    {
        var body = Body(await CreateSut().InvokeAsync(Search, Call(), Catalog, Cancel));

        Assert.Null(body["fieldDescriptions"]);
    }

    [Fact]
    public async Task AStepResult_CarriesTheDescriptionsOfItsFields()
    {
        var start = Start with { OutputDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["VisitId"] = "The visit opened." } };
        StepReturns(StepResponse() with { Result = JsonSerializer.SerializeToElement(new { visitId = "v-1" }) });

        var body = Body(await CreateSut().InvokeAsync(start, Call(), new ToolCatalog { Tools = [start, Close, Answer, State] }, Cancel));

        Assert.Equal("The visit opened.", body["fieldDescriptions"]?["visitId"]?.GetValue<string>());
    }

    // ── Questions: conditional (EnableWhen, ADR 0024) ─────────────────────────
    // The adapter asks one round at a time - only the questions the answers so far decide - and
    // submits once; the agent and the form never see the conditions.

    private static readonly InformationRequest ConditionalQuestions = new()
    {
        InformationRequestId = "visit-intake",
        Items =
        [
            new InformationItem { Id = "urgent", Text = "Urgent?", Type = InformationItemType.Boolean },
            new InformationItem
            {
                Id = "reason",
                Text = "Why is it urgent?",
                Type = InformationItemType.Text,
                EnableWhen = [new InformationCondition { Question = "urgent", Operator = InformationConditionOperator.Equals, Answer = "true" }],
            },
        ]
    };

    private static readonly ProcessRequiredStep ConditionalRequired = QuestionsRequired with { InformationRequest = ConditionalQuestions };

    private readonly List<InformationRequest> roundForms = [];

    private static InformationResponseItem[] Answers(string itemId, string value) =>
        [new InformationResponseItem { ItemId = itemId, Answers = [new InformationAnswer { Value = value }] }];

    // The form of each round, and the user's answers to it in order.
    private void FormsAnswer(params InformationResponseItem[][] rounds)
    {
        elicitor.Setup(x => x.ToElicitation(It.IsAny<InformationRequest>()))
            .Callback((InformationRequest round) => roundForms.Add(round))
            .Returns(new ElicitRequestParams { Message = "Visit intake", RequestedSchema = new ElicitRequestParams.RequestSchema() });

        var sequence = elicitor.SetupSequence(x => x.ToAnswers(It.IsAny<InformationRequest>(), It.IsAny<IReadOnlyDictionary<string, JsonElement>>()));

        foreach (var round in rounds)
        {
            sequence = sequence.Returns(round);
        }
    }

    private static string[] Ids(InformationRequest round) => [.. round.Items.Select(x => x.Id)];

    [Fact]
    public async Task ConditionalQuestions_WithMrtr_AskTheFollowUpOnlyWhenTheAnswerEnablesIt_AndSubmitOnce()
    {
        FormsAnswer(Answers("urgent", "true"), Answers("reason", "Chest pain"));
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: ConditionalRequired));
        var first = (await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel)).InputRequired?.RequestState ?? throw new Xunit.Sdk.XunitException("No form.");
        posts.Clear();
        StateIs("clinic", StateResponse(ConditionalRequired));

        var second = (await CreateSut().InvokeAsync(Start, Call(requestState: first, inputResponses: Answered("accept")), Catalog, Cancel)).InputRequired?.RequestState
            ?? throw new Xunit.Sdk.XunitException("No follow-up form.");

        Assert.Empty(posts);
        Assert.Equal([["urgent"], ["reason"]], roundForms.Select(Ids));
        Assert.All(roundForms, round => Assert.All(round.Items, item => Assert.Empty(item.EnableWhen)));

        StepReturns(StepResponse(required: CloseRequired));
        await CreateSut().InvokeAsync(Start, Call(requestState: second, inputResponses: Answered("accept")), Catalog, Cancel);

        var submitted = Assert.Single(posts).Body["items"]?.AsArray() ?? throw new Xunit.Sdk.XunitException("No items.");
        Assert.Equal(["urgent", "reason"], submitted.Select(x => x?["itemId"]?.GetValue<string>()));
        Assert.Equal("Chest pain", submitted[1]?["answers"]?[0]?["value"]?.GetValue<string>());
    }

    [Fact]
    public async Task ConditionalQuestions_WithMrtr_WhenTheAnswerDisablesTheFollowUp_SubmitAtOnce()
    {
        FormsAnswer(Answers("urgent", "false"));
        var state = await MrtrStateAsync(ConditionalRequired);
        StateIs("clinic", StateResponse(ConditionalRequired));
        StepReturns(StepResponse(required: CloseRequired));

        var outcome = await CreateSut().InvokeAsync(Start, Call(requestState: state, inputResponses: Answered("accept")), Catalog, Cancel);

        Assert.Null(outcome.InputRequired);
        Assert.Equal(["urgent"], Assert.Single(posts).Body["items"]?.AsArray().Select(x => x?["itemId"]?.GetValue<string>()));
    }

    [Fact]
    public async Task ConditionalQuestions_InASession_AreAskedRoundByRound_AndSubmittedOnce()
    {
        FormsAnswer(Answers("urgent", "true"), Answers("reason", "Chest pain"));
        UserAnswersInASession(Accepted(), Accepted());
        StepsReturn(StepResponse(required: ConditionalRequired), StepResponse(required: CloseRequired));

        await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel);

        Assert.Equal(2, sessionForms.Count);
        Assert.Equal([["urgent"], ["reason"]], roundForms.Select(Ids));
        Assert.Equal(["urgent", "reason"], posts[1].Body["items"]?.AsArray().Select(x => x?["itemId"]?.GetValue<string>()));
    }

    [Fact]
    public async Task ConditionalQuestions_InASession_DecliningTheFollowUp_SubmitsNothing()
    {
        FormsAnswer(Answers("urgent", "true"));
        UserAnswersInASession(Accepted(), Declined());
        StepReturns(StepResponse(required: ConditionalRequired));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Single(posts);
        Assert.Contains("(decline)", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    // A form accepted without answers to its questions ends the rounds instead of repeating the form.
    [Fact]
    public async Task ConditionalQuestions_InASession_AFollowUpAcceptedWithoutAnswers_SubmitsNothing()
    {
        FormsAnswer(Answers("urgent", "true"), []);
        UserAnswersInASession(Accepted(), Accepted());
        StepReturns(StepResponse(required: ConditionalRequired));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        Assert.Equal(2, sessionForms.Count);
        Assert.Single(posts);
        Assert.Contains("did not answer", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    // Chat: the agent gets only the questions that apply now, never the conditions.
    [Fact]
    public async Task ConditionalQuestions_ForTheAgent_ListOnlyTheQuestionsThatApplyNow()
    {
        StepReturns(StepResponse(required: ConditionalRequired));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(), Catalog, Cancel));

        var items = body["next"]?["informationRequest"]?["items"]?.AsArray() ?? throw new Xunit.Sdk.XunitException("No questions.");
        Assert.Equal("urgent", Assert.Single(items)?["id"]?.GetValue<string>());
        Assert.Null(body["next"]?[KaleidoToolInvoker.AnswersTokenArgument]);
        Assert.DoesNotContain("enableWhen", body.ToJsonString(), StringComparison.Ordinal);
    }

    private KaleidoToolCall AnswerCall(string itemId, string value, string? answersToken = null) =>
        Call(new Dictionary<string, object?>
        {
            ["service"] = "clinic",
            ["processId"] = ProcessId,
            ["informationRequestId"] = "visit-intake",
            ["answers"] = new[] { new { itemId, values = new List<string> { value } } },
            [KaleidoToolInvoker.AnswersTokenArgument] = answersToken,
        });

    [Fact]
    public async Task ConditionalQuestions_ForTheAgent_ReturnTheFollowUpWithAToken_ThenSubmitEveryAnswer()
    {
        StateIs("clinic", StateResponse(ConditionalRequired));

        var first = Body(await CreateSut().InvokeAsync(Answer, AnswerCall("urgent", "true"), Catalog, Cancel));

        Assert.Empty(posts);
        Assert.Equal("reason", Assert.Single(first["next"]?["informationRequest"]?["items"]?.AsArray() ?? [])?["id"]?.GetValue<string>());
        Assert.Contains("answersToken", first["next"]?["instruction"]?.GetValue<string>(), StringComparison.Ordinal);
        var token = first["next"]?[KaleidoToolInvoker.AnswersTokenArgument]?.GetValue<string>() ?? throw new Xunit.Sdk.XunitException("No token.");

        await CreateSut().InvokeAsync(Answer, AnswerCall("reason", "Chest pain", token), Catalog, Cancel);

        var submitted = Assert.Single(posts).Body["items"]?.AsArray() ?? throw new Xunit.Sdk.XunitException("No items.");
        Assert.Equal(["urgent", "reason"], submitted.Select(x => x?["itemId"]?.GetValue<string>()));
        Assert.Equal("true", submitted[0]?["answers"]?[0]?["value"]?.GetValue<string>());
    }

    // An answer to a question the other answers disable is not sent (Kaleido would refuse it).
    [Fact]
    public async Task ConditionalQuestions_ForTheAgent_AnAnswerToADisabledQuestion_IsNotSubmitted()
    {
        StateIs("clinic", StateResponse(ConditionalRequired));
        var call = Call(new
        {
            service = "clinic",
            processId = ProcessId,
            informationRequestId = "visit-intake",
            answers = new[] { new { itemId = "urgent", values = new List<string> { "false" } }, new { itemId = "reason", values = new List<string> { "None" } } },
        });

        await CreateSut().InvokeAsync(Answer, call, Catalog, Cancel);

        Assert.Equal(["urgent"], Assert.Single(posts).Body["items"]?.AsArray().Select(x => x?["itemId"]?.GetValue<string>()));
    }

    // A token is only read for the process and request it was issued for.
    [Theory]
    [InlineData("another-request")]
    [InlineData("unreadable")]
    public async Task ConditionalQuestions_ForTheAgent_ATokenForSomethingElse_IsIgnored(string kind)
    {
        StateIs("clinic", StateResponse(ConditionalRequired));
        var first = Body(await CreateSut().InvokeAsync(Answer, AnswerCall("urgent", "true"), Catalog, Cancel));
        var token = first["next"]?[KaleidoToolInvoker.AnswersTokenArgument]?.GetValue<string>() ?? throw new Xunit.Sdk.XunitException("No token.");
        var changed = kind == "unreadable"
            ? "kaleido-answers:%%%"
            : Recode(token, "kaleido-answers:", json => json["informationRequestId"] = "another");

        await CreateSut().InvokeAsync(Answer, AnswerCall("reason", "Chest pain", changed), Catalog, Cancel);

        // Without the earlier answers "urgent" is unanswered, so "reason" does not apply and is not sent;
        // Kaleido then reports the unanswered question, as for any incomplete answers.
        Assert.Empty(Assert.Single(posts).Body["items"]?.AsArray() ?? throw new Xunit.Sdk.XunitException("No items."));
    }

    [Fact]
    public async Task TheMrtrRetry_WithoutAnswers_SubmitsNothing_AndSaysSo()
    {
        var state = await MrtrStateAsync();
        StateIs("clinic", StateResponse(QuestionsRequired));

        var unanswered = Body(await CreateSut().InvokeAsync(Start, Call(requestState: state), Catalog, Cancel));
        var otherInput = Body(await CreateSut().InvokeAsync(Start, Call(requestState: state, inputResponses: LookupAnswered()), Catalog, Cancel));

        Assert.Empty(posts);
        Assert.Contains("(no answer)", unanswered["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("(no answer)", otherInput["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMrtrRetry_WhenTheProcessIsNoLongerWaiting_SaysSo()
    {
        var state = await MrtrStateAsync();
        StateIs("clinic", StateResponse(CloseRequired, state: ProcessExecutionState.Active));

        var body = Body(await CreateSut().InvokeAsync(Start, Call(requestState: state, inputResponses: Answered("accept")), Catalog, Cancel));

        Assert.Empty(posts);
        Assert.Equal(McpErrorCodes.NoPendingInformationRequest, body["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task AContinuationStateThatIsNotItsShape_IsAnArgumentError()
    {
        var notJson = "kaleido-mcp:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("not json"));
        var nullState = "kaleido-mcp:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("null"));

        Assert.Equal(McpErrorCodes.InvalidArguments, Body(await CreateSut().InvokeAsync(Start, Call(requestState: notJson), Catalog, Cancel))["code"]?.GetValue<string>());
        Assert.Equal(McpErrorCodes.InvalidArguments, Body(await CreateSut().InvokeAsync(Start, Call(requestState: nullState), Catalog, Cancel))["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task ARequestStateOfAnotherServer_IsIgnored()
    {
        await CreateSut().InvokeAsync(Start, Call(requestState: "other-server:abc"), Catalog, Cancel);

        Assert.Single(posts);
    }

    // ── answer_information_request / get_process_state: shapes ───────────────

    [Fact]
    public async Task AnswerInformationRequest_TakesAnswersOfAnyShape()
    {
        StateIs("clinic", StateResponse(QuestionsRequired));

        await CreateSut().InvokeAsync(
            Answer,
            Call(JsonNode.Parse($$"""
                {
                  "service": "clinic",
                  "processId": "{{ProcessId}}",
                  "informationRequestId": "visit-intake",
                  "answers": [
                    { "itemId": "urgent", "values": [true, 3, "x"] },
                    { "itemId": "note", "values": "x" },
                    { "values": ["y"] },
                    { "itemId": "empty" }
                  ]
                }
                """)),
            Catalog,
            Cancel);

        var items = Assert.Single(posts).Body["items"]?.AsArray() ?? [];
        Assert.Equal(["urgent", "note", "", "empty"], items.Select(x => x?["itemId"]?.GetValue<string>()));
        Assert.Equal(["true", "3", "x"], items[0]?["answers"]?.AsArray().Select(x => x?["value"]?.GetValue<string>()));
        Assert.Empty(items[1]?["answers"]?.AsArray() ?? [new JsonObject()]);
        Assert.Equal("y", items[2]?["answers"]?[0]?["value"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnswerInformationRequest_WithoutAnAnswerList_SubmitsNoAnswers()
    {
        StateIs("clinic", StateResponse(QuestionsRequired));

        await CreateSut().InvokeAsync(
            Answer,
            Call(new { service = "clinic", processId = ProcessId, informationRequestId = "visit-intake", answers = new { urgent = true } }),
            Catalog,
            Cancel);

        Assert.Empty(Assert.Single(posts).Body["items"]?.AsArray() ?? [new JsonObject()]);
    }

    [Theory]
    [InlineData("""{"service":"clinic","processId":"nope","informationRequestId":"visit-intake"}""")]
    [InlineData("""{"service":"clinic","processId":"11111111-2222-3333-4444-555555555555"}""")]
    [InlineData("""{"processId":"11111111-2222-3333-4444-555555555555","informationRequestId":"visit-intake"}""")]
    public async Task AnswerInformationRequest_WithoutItsIdentifiers_IsAnArgumentError(string arguments)
    {
        var body = Body(await CreateSut().InvokeAsync(Answer, Call(JsonNode.Parse(arguments)), Catalog, Cancel));

        Assert.Equal(McpErrorCodes.InvalidArguments, body["code"]?.GetValue<string>());
        Assert.Equal("Pass service, processId, informationRequestId and answers.", body["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnswerInformationRequest_ForAProcessThatCannotBeRead_SaysNothingIsPending()
    {
        var body = Body(await CreateSut().InvokeAsync(
            Answer,
            Call(new { service = "clinic", processId = ProcessId, informationRequestId = "visit-intake" }),
            Catalog,
            Cancel));

        Assert.Equal(McpErrorCodes.NoPendingInformationRequest, body["code"]?.GetValue<string>());
        Assert.Empty(posts);
    }

    [Theory]
    [InlineData("""{"service":"clinic"}""")]
    [InlineData("""{"service":"clinic","processId":"nope"}""")]
    [InlineData("""{"processId":"11111111-2222-3333-4444-555555555555"}""")]
    public async Task GetProcessState_WithoutItsIdentifiers_IsAnArgumentError(string arguments)
    {
        var body = Body(await CreateSut().InvokeAsync(State, Call(JsonNode.Parse(arguments)), Catalog, Cancel));

        Assert.Equal(McpErrorCodes.InvalidArguments, body["code"]?.GetValue<string>());
        Assert.Equal("Pass service and processId.", body["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetProcessState_OfAProcessThatCannotBeRead_IsAServiceError()
    {
        var body = Body(await CreateSut().InvokeAsync(State, Call(new { service = "clinic", processId = ProcessId }), Catalog, Cancel));

        Assert.Equal(McpErrorCodes.ServiceError, body["code"]?.GetValue<string>());
        Assert.Equal($"Process {ProcessId} was not found on 'clinic', or you may not see it.", body["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetProcessState_ListsTheFailedSteps_AndHasNoNote()
    {
        StateIs("clinic", StateResponse(CloseRequired, state: ProcessExecutionState.Active) with
        {
            Steps =
            [
                new ProcessStepHistory { StepName = "StartVisitStep", Status = StepExecutionStatus.Completed },
                new ProcessStepHistory { StepName = "CheckInStep", Status = StepExecutionStatus.Failed },
                new ProcessStepHistory { StepName = "ScanStep", Status = StepExecutionStatus.Exception }
            ]
        });

        var body = Body(await CreateSut().InvokeAsync(State, Call(new { service = "clinic", processId = ProcessId }), Catalog, Cancel));

        Assert.Equal(["CheckInStep", "ScanStep"], body["failedSteps"]?.AsArray().Select(x => x?.GetValue<string>()));
        Assert.Equal(["StartVisitStep"], body["completedSteps"]?.AsArray().Select(x => x?.GetValue<string>()));
        Assert.Equal("clinic_close_visit", body["next"]?["required"]?["tool"]?.GetValue<string>());
        Assert.Null(body["note"]);
    }

    // ── Front door: details ──────────────────────────────────────────────────

    private static readonly KaleidoTool HiddenScan = Scan with
    {
        Definition = new Tool
        {
            Name = "radiology_capture_scan",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
        },
        Listed = false,
        Lookups =
        [
            new ToolLookup { Group = "order", Service = "radiology", Source = "Order", View = "OpenOrders", Inputs = [("orderId", "Id")] },
            new ToolLookup { Group = "room", Service = "radiology", Source = "Room", QueryTool = "radiology_rooms", Inputs = [("roomId", "Id")] },
            new ToolLookup { Group = "patient", Service = "clinic", Source = "Patient", QueryTool = "clinic_patient", Inputs = [("patientId", "Id")] }
        ]
    };

    private static readonly ToolCatalog DetailsCatalog = new()
    {
        Tools = [Search, Start, Close, HiddenScan],
        IsFrontDoor = true
    };

    [Fact]
    public async Task FrontDoor_Next_DescribesAnUnlistedAvailableStep_AndItsLookups()
    {
        StepReturns(StepResponse(available: [new ProcessStepSummary { Name = "CaptureScanStep", ExecuteUrl = Scan.Url }]));

        var next = Body(await CreateSut().InvokeAsync(Start, Call(), DetailsCatalog, Cancel))["next"];
        var scan = next?["available"]?[0];

        Assert.Contains("call_tool", next?["howToRun"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(scan?["description"]);
        Assert.Equal(false, scan?["hints"]?["readOnly"]?.GetValue<bool>());
        Assert.Equal(false, scan?["hints"]?["destructive"]?.GetValue<bool>());
        Assert.Equal(false, scan?["hints"]?["idempotent"]?.GetValue<bool>());

        var lookups = scan?["lookups"]?.AsArray() ?? [];
        Assert.Equal("These come from OpenOrders on radiology, which you cannot search: ask the user.", lookups[0]?["note"]?.GetValue<string>());
        Assert.Equal("These come from Room on radiology, which you cannot search: ask the user.", lookups[1]?["note"]?.GetValue<string>());
        Assert.Equal("Id", lookups[1]?["fills"]?["roomId"]?.GetValue<string>());
        Assert.Equal("clinic_patient", lookups[2]?["search"]?["tool"]?.GetValue<string>());
        Assert.Null(lookups[2]?["search"]?["show"]);
        Assert.Equal(ProcessId.ToString(), lookups[2]?["search"]?["arguments"]?["processId"]?.GetValue<string>());
    }

    [Fact]
    public async Task FrontDoor_Next_WithOnlyListedSteps_SaysNothingAboutRunningThem()
    {
        StepReturns(StepResponse(required: CloseRequired, available: [new ProcessStepSummary { Name = "StartVisitStep", ExecuteUrl = Start.Url }]));

        var next = Body(await CreateSut().InvokeAsync(Start, Call(), DetailsCatalog, Cancel))["next"];

        Assert.Null(next?["howToRun"]);
        Assert.Null(next?["required"]?["inputs"]);
    }

    private static readonly KaleidoTool SearchToolsTool = new() { Definition = new Tool { Name = ToolCatalogBuilder.SearchToolsTool }, Kind = KaleidoToolKind.SearchTools };

    [Theory]
    [InlineData("query", new[] { "clinic_patient" })]
    [InlineData("STEP", new[] { "clinic_close_visit" })]
    [InlineData(null, new[] { "clinic_patient", "clinic_close_visit" })]
    public async Task SearchTools_CanBeLimitedToQueriesOrSteps(string? kind, string[] expected)
    {
        var catalog = FrontDoorCatalog with
        {
            Tools = [Search with { Definition = new Tool { Name = "clinic_patient", Title = "Patients", Description = "Find a patient." } }, HiddenClose, Answer]
        };

        var body = Body(await CreateSut().InvokeAsync(SearchToolsTool, Call(new { text = "clinic, patient", kind }), catalog, Cancel));

        Assert.Equal(expected, body["tools"]?.AsArray().Select(x => x?["name"]?.GetValue<string>()));
        Assert.Contains("describe_tool", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchTools_ThatMatchNothing_SaysSo()
    {
        var body = Body(await CreateSut().InvokeAsync(SearchToolsTool, Call(new { text = "invoice" }), FrontDoorCatalog, Cancel));

        Assert.Empty(body["tools"]?.AsArray() ?? [new JsonObject()]);
        Assert.Equal("No tool matched. Try other words, or check next for the steps that are open now.", body["note"]?.GetValue<string>());
    }

    [Fact]
    public async Task SearchTools_RanksByMatchedWords_AndListsRequiredInputs()
    {
        var required = Close with
        {
            Definition = new Tool
            {
                Name = "clinic_close_visit",
                Description = "Close the visit.",
                InputSchema = JsonSerializer.Deserialize<JsonElement>("""{"type":"object","required":["processId"]}""")
            }
        };
        var catalog = Catalog with { Tools = [Search, required] };

        var body = Body(await CreateSut().InvokeAsync(SearchToolsTool, Call(new { text = "close visit clinic" }), catalog, Cancel));

        var tools = body["tools"]?.AsArray() ?? [];
        Assert.Equal(["clinic_close_visit", "clinic_patient"], tools.Select(x => x?["name"]?.GetValue<string>()));
        Assert.Equal(["processId"], tools[0]?["required"]?.AsArray().Select(x => x?.GetValue<string>()));
        Assert.Equal("step", tools[0]?["kind"]?.GetValue<string>());
        Assert.Equal("query", tools[1]?["kind"]?.GetValue<string>());
    }

    private static readonly KaleidoTool DescribeToolTool = new() { Definition = new Tool { Name = ToolCatalogBuilder.DescribeToolTool }, Kind = KaleidoToolKind.DescribeTool };

    [Fact]
    public async Task DescribeTool_WithoutAName_IsAnUnknownToolResult()
    {
        var body = Body(await CreateSut().InvokeAsync(DescribeToolTool, Call(), FrontDoorCatalog, Cancel));

        Assert.Equal(McpErrorCodes.UnknownTool, body["code"]?.GetValue<string>());
        Assert.Equal("describe_tool: there is no tool '' for you. Find tools with search_tools.", body["message"]?.GetValue<string>());
    }

    [Fact]
    public async Task DescribeTool_OfAListedTool_HasNoHowToRun()
    {
        var body = Body(await CreateSut().InvokeAsync(DescribeToolTool, Call(new { name = "clinic_patient" }), FrontDoorCatalog, Cancel));

        Assert.True(body["listed"]?.GetValue<bool>());
        Assert.Null(body["howToRun"]);
        Assert.Null(body["description"]);
        Assert.Null(body["lookups"]);
        Assert.Equal(false, body["hints"]?["readOnly"]?.GetValue<bool>());
    }

    // ── Guided lookups: shapes ───────────────────────────────────────────────

    private readonly List<InformationRequest> forms = [];
    private string? searchAnswer = "Ava";
    private string? pickAnswer = "0";

    private void LookupAnswers()
    {
        elicitor.Setup(x => x.ToElicitation(It.IsAny<InformationRequest>()))
            .Callback((InformationRequest request) => forms.Add(request))
            .Returns((InformationRequest request) => new ElicitRequestParams { Message = request.InformationRequestId, RequestedSchema = new ElicitRequestParams.RequestSchema() });
        elicitor.Setup(x => x.ToAnswers(It.IsAny<InformationRequest>(), It.IsAny<IReadOnlyDictionary<string, JsonElement>>()))
            .Returns((InformationRequest request, IReadOnlyDictionary<string, JsonElement> _) =>
                request.InformationRequestId.StartsWith("lookup-search", StringComparison.Ordinal)
                    ? searchAnswer is null ? [] : [new InformationResponseItem { ItemId = "search", Answers = [new InformationAnswer { Value = searchAnswer }] }]
                    : pickAnswer is null ? [] : [new InformationResponseItem { ItemId = "result", Answers = [new InformationAnswer { Value = pickAnswer }] }]);
    }

    private void PatientsFound(params JsonObject[] rows) => PatientsFound(rows.Length, rows);

    private void PatientsFound(int total, params JsonObject[] rows)
    {
        clinicQueries.Setup(x => x.QuerySourceAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .Callback((string source, QueryApiRequest<JsonObject> request, CancellationToken _) => queries.Add((source, null, request)))
            .ReturnsAsync(() => new QueryResult<JsonObject>(total, 0, 10, [.. rows.Select(x => x.DeepClone().AsObject())]));
        clinicQueries.Setup(x => x.QueryViewAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .Callback((string source, string view, QueryApiRequest<JsonObject> request, CancellationToken _) => queries.Add((source, view, request)))
            .ReturnsAsync(() => new QueryResult<JsonObject>(total, 0, 10, [.. rows.Select(x => x.DeepClone().AsObject())]));
    }

    private static readonly JsonObject Ava = new() { ["patientId"] = "P-1", ["enrollmentId"] = "E-1", ["name"] = "Ava Carter" };

    private void MrtrLookupForms()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        LookupAnswers();
    }

    private async Task<string> LookupSearchStateAsync(KaleidoTool tool, ToolCatalog catalog, object? arguments = null) =>
        (await CreateSut().InvokeAsync(tool, Call(arguments ?? new { processId = ProcessId }), catalog, Cancel)).InputRequired?.RequestState
        ?? throw new Xunit.Sdk.XunitException("No lookup state.");

    [Fact]
    public async Task ALookupWhoseInputsAreBlank_IsGuided()
    {
        MrtrLookupForms();

        var outcome = await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId, patientId = (string?)null, enrollmentId = " " }), LookupCatalog, Cancel);

        Assert.StartsWith("kaleido-mcp-lookup:", outcome.InputRequired?.RequestState, StringComparison.Ordinal);
        var search = Assert.Single(forms);
        Assert.Equal("lookup-search|patient", search.InformationRequestId);
        Assert.Equal("Find Patient Search", search.Title);
        Assert.Equal("Search Patient Search: enter details that identify the record (e.g. Name).", search.Items[0].Text);
    }

    [Fact]
    public async Task ALookupTheCallerCannotSearch_IsNotGuided()
    {
        MrtrLookupForms();
        var unsearchable = PickPatient with { Lookups = [PickPatient.Lookups[0] with { QueryTool = null }] };

        await CreateSut().InvokeAsync(unsearchable, Call(new { processId = ProcessId }), LookupCatalog, Cancel);

        Assert.Single(posts);
        Assert.Empty(forms);
    }

    [Fact]
    public async Task TheSearchRetry_WithManyResults_SaysHowManyThereAre_AndLabelsThemByTheirDisplayFields()
    {
        MrtrLookupForms();
        PatientsFound(25, Ava, new JsonObject { ["patientId"] = "P-2", ["enrollmentId"] = "E-2" });
        var searchState = await LookupSearchStateAsync(PickPatient, LookupCatalog);

        await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        var pick = forms[^1];
        Assert.Equal("lookup-pick|patient", pick.InformationRequestId);
        Assert.Equal("Choose the Patient Search result", pick.Title);
        Assert.Equal("Showing the first 2 of 25 results. Pick one, or search again with more details.", pick.Items[0].Text);
        Assert.Equal(["Ava Carter", "", "None of these: search again"], pick.Items[0].Options.Select(x => x.Display));
        Assert.Equal(["0", "1", "search-again"], pick.Items[0].Options.Select(x => x.Value));
        Assert.Equal(10, Assert.Single(queries).Request.Query?.Page?.Size);
    }

    [Fact]
    public async Task TheSearchRetry_WithoutResults_AsksToSearchAgain_WithANote()
    {
        MrtrLookupForms();
        PatientsFound();
        var searchState = await LookupSearchStateAsync(PickPatient, LookupCatalog);

        var outcome = await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        Assert.Equal(searchState, outcome.InputRequired?.RequestState);
        Assert.StartsWith("No Patient Search result matched \"Ava\". Try other details. Search Patient Search", forms[^1].Items[0].Text, StringComparison.Ordinal);
        Assert.Empty(posts);
    }

    [Fact]
    public async Task TheSearchRetry_WithoutASearchText_RunsNothing()
    {
        MrtrLookupForms();
        searchAnswer = null;
        var searchState = await LookupSearchStateAsync(PickPatient, LookupCatalog);

        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel));

        Assert.Equal("notRun", body["outcome"]?.GetValue<string>());
        Assert.Equal("PickPatientStep", body["step"]?.GetValue<string>());
        Assert.Equal(
            "The user did not enter anything to search for. Nothing was run. clinic_pick_patient needs patientId and enrollmentId from one Patient Search result.",
            body["note"]?.GetValue<string>());
        Assert.Empty(queries);
    }

    [Fact]
    public async Task TheLookupRetry_WithoutAnAnswer_RunsNothing()
    {
        MrtrLookupForms();
        var searchState = await LookupSearchStateAsync(PickPatient, LookupCatalog);
        var declined = new Dictionary<string, InputResponse>(StringComparer.Ordinal)
        {
            ["kaleido-lookup"] = new() { RawValue = JsonSerializer.SerializeToElement(new ElicitResult { Action = "cancel" }, McpJsonUtilities.DefaultOptions) }
        };

        var unanswered = Body(await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState), LookupCatalog, Cancel));
        var otherInput = Body(await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: Answered("accept")), LookupCatalog, Cancel));
        var canceled = Body(await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: declined), LookupCatalog, Cancel));

        Assert.StartsWith("The user did not finish the lookup (no answer).", unanswered["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("The user did not finish the lookup (no answer).", otherInput["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("The user did not finish the lookup (cancel).", canceled["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(queries);
        Assert.Empty(posts);
    }

    [Theory]
    [InlineData("search-again")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData(null)]
    public async Task ThePickRetry_WithoutAValidChoice_AsksToSearchAgain(string? choice)
    {
        MrtrLookupForms();
        PatientsFound(Ava);
        var searchState = await LookupSearchStateAsync(PickPatient, LookupCatalog);
        var pickState = (await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel)).InputRequired?.RequestState
            ?? throw new Xunit.Sdk.XunitException("No pick state.");
        pickAnswer = choice;

        var outcome = await CreateSut().InvokeAsync(PickPatient, Call(requestState: pickState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        var again = JsonNode.Parse(Decode(outcome.InputRequired?.RequestState ?? throw new Xunit.Sdk.XunitException("No state."), "kaleido-mcp-lookup:"));
        Assert.Null(again?["searchText"]);
        Assert.Empty(again?["candidates"]?.AsArray() ?? [new JsonObject()]);
        Assert.Equal("lookup-search|patient", forms[^1].InformationRequestId);
        Assert.Empty(posts);
    }

    [Fact]
    public async Task ASessionLookup_FillsOnlyTheInputsTheResultHas()
    {
        SessionLookupForms(Accepted(), Accepted());
        PatientsFound(new JsonObject { ["patientId"] = "P-3", ["name"] = "Bo" });

        await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel);

        var body = Assert.Single(posts).Body;
        Assert.Equal("P-3", body["patientId"]?.GetValue<string>());
        Assert.Null(body["enrollmentId"]);
    }

    [Fact]
    public async Task AnMrtrLookup_OffersAResultThatLacksAnInput_AndFillsOnlyTheInputsItHas()
    {
        LookupForms();
        PatientsFound(new JsonObject { ["patientId"] = "P-3", ["name"] = "Bo" });
        var searchState = (await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel)).InputRequired?.RequestState;

        var pick = await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        Assert.StartsWith("kaleido-mcp-lookup:", Assert.IsType<InputRequiredResult>(pick.InputRequired).RequestState, StringComparison.Ordinal);

        await CreateSut().InvokeAsync(PickPatient, Call(requestState: pick.InputRequired?.RequestState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        var body = Assert.Single(posts).Body;
        Assert.Equal("P-3", body["patientId"]?.GetValue<string>());
        Assert.Null(body["enrollmentId"]);
    }

    [Theory]
    [InlineData("kaleido-mcp-lookup:%%%")]
    [InlineData("kaleido-mcp-lookup:bnVsbA==")]
    public async Task AnUnreadableLookupState_IsAnArgumentError(string state)
    {
        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(requestState: state), LookupCatalog, Cancel));

        Assert.Equal(McpErrorCodes.InvalidArguments, body["code"]?.GetValue<string>());
        Assert.Equal("The lookup state is not readable; call the tool again.", body["message"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("tool", "clinic_unknown")]
    [InlineData("tool", "clinic_patient")]
    [InlineData("group", "nope")]
    public async Task ALookupStateForNoLookupOfTheCaller_IsAnArgumentError(string key, string value)
    {
        MrtrLookupForms();
        var state = Recode(await LookupSearchStateAsync(PickPatient, LookupCatalog), "kaleido-mcp-lookup:", x => x[key] = value);

        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(requestState: state, inputResponses: LookupAnswered()), LookupCatalog, Cancel));

        Assert.Equal(McpErrorCodes.InvalidArguments, body["code"]?.GetValue<string>());
        Assert.Empty(queries);
    }

    [Fact]
    public async Task ALookupStateWithoutArguments_RunsTheStepWithOnlyTheFilledInputs()
    {
        MrtrLookupForms();
        PatientsFound(Ava);
        var searchState = Recode(await LookupSearchStateAsync(PickPatient, LookupCatalog), "kaleido-mcp-lookup:", x => x["arguments"] = "null");
        var pickState = (await CreateSut().InvokeAsync(PickPatient, Call(requestState: searchState, inputResponses: LookupAnswered()), LookupCatalog, Cancel)).InputRequired?.RequestState;

        await CreateSut().InvokeAsync(PickPatient, Call(requestState: pickState, inputResponses: LookupAnswered()), LookupCatalog, Cancel);

        // Only the filled inputs, in the canonical (ordinal) order every step body is sent in (#440).
        var body = Assert.Single(posts).Body.AsObject();
        Assert.Equal(["enrollmentId", "patientId"], body.Select(x => x.Key));
    }

    private static readonly KaleidoTool ScopedRooms = new()
    {
        Definition = new Tool { Name = "clinic_rooms", Title = "Rooms" },
        Kind = KaleidoToolKind.Query,
        Service = "clinic",
        SourceName = "Room",
        ParametersTakeProcessId = true
    };

    private static readonly KaleidoTool BookRoom = new()
    {
        Definition = new Tool { Name = "clinic_book_room" },
        Kind = KaleidoToolKind.Step,
        Service = "clinic",
        CapabilityName = "BookRoomStep",
        IsInitial = true,
        Lookups =
        [
            new ToolLookup { Group = "room", Service = "clinic", Source = "Room", View = "FreeRooms", QueryTool = "clinic_rooms", Inputs = [("roomId", "RoomId")] },
            new ToolLookup { Group = "ward", Service = "clinic", Source = "Ward", Inputs = [("wardId", "WardId"), ("isInNetwork", "IsInNetwork"), ("isOpen", "IsOpen"), ("is", "Is")] }
        ]
    };

    private static readonly ToolCatalog RoomCatalog = Catalog with { Tools = [.. Catalog.Tools, ScopedRooms, BookRoom] };

    [Fact]
    public async Task ALookupThroughAView_SearchesTheView_ScopedToTheProcess()
    {
        MrtrLookupForms();
        PatientsFound(new JsonObject { ["roomId"] = "R-1" });
        var searchState = await LookupSearchStateAsync(BookRoom, RoomCatalog);

        await CreateSut().InvokeAsync(BookRoom, Call(requestState: searchState, inputResponses: LookupAnswered()), RoomCatalog, Cancel);

        var (source, view, request) = Assert.Single(queries);
        Assert.Equal("Room", source);
        Assert.Equal("FreeRooms", view);
        Assert.Equal(ProcessId.ToString(), request.Parameters?["processId"]?.GetValue<string>());
        Assert.Equal([ProcessId], forwardedProcessIds);
        Assert.Equal("Find Room", forms[0].Title);
        Assert.Equal("Search Room: enter details that identify the record.", forms[0].Items[0].Text);
        Assert.Equal("Pick the result that is the right record, or search again.", forms[^1].Items[0].Text);
        Assert.Equal("R-1", forms[^1].Items[0].Options[0].Display);
    }

    [Fact]
    public async Task ALookupForANewProcess_IsNotRecordedWithAProcess()
    {
        MrtrLookupForms();
        PatientsFound(new JsonObject { ["roomId"] = "R-1" });
        var searchState = await LookupSearchStateAsync(BookRoom, RoomCatalog, new { });

        await CreateSut().InvokeAsync(BookRoom, Call(requestState: searchState, inputResponses: LookupAnswered()), RoomCatalog, Cancel);

        Assert.Null(Assert.Single(queries).Request.Parameters);
        Assert.Empty(forwardedProcessIds);
    }

    [Fact]
    public async Task ALookupStateOfALookupWithoutASearch_SearchesItsSource_AndLabelsResultsByTheirInputs()
    {
        MrtrLookupForms();
        clinicQueries.Setup(x => x.QuerySourceAsync<JsonObject, JsonObject>(It.IsAny<string>(), It.IsAny<QueryApiRequest<JsonObject>>(), It.IsAny<CancellationToken>()))
            .Callback((string source, QueryApiRequest<JsonObject> request, CancellationToken _) => queries.Add((source, null, request)))
            .ReturnsAsync(new QueryResult<JsonObject>(1, 0, 10, [new JsonObject { ["wardId"] = "W-1", ["isInNetwork"] = true, ["isOpen"] = false, ["is"] = true }]));
        var searchState = Recode(await LookupSearchStateAsync(BookRoom, RoomCatalog), "kaleido-mcp-lookup:", x => x["group"] = "ward");

        await CreateSut().InvokeAsync(BookRoom, Call(requestState: searchState, inputResponses: LookupAnswered()), RoomCatalog, Cancel);

        var (source, view, request) = Assert.Single(queries);
        Assert.Equal("Ward", source);
        Assert.Null(view);
        Assert.Null(request.Parameters);
        Assert.Equal("Choose the Ward result", forms[^1].Title);
        Assert.Equal("W-1 · In network · not open · Is", forms[^1].Items[0].Options[0].Display);
    }

    // ── Guided lookups: session elicitation ──────────────────────────────────

    private void SessionLookupForms(params ElicitResult[] answers)
    {
        UserAnswersInASession(answers);
        LookupAnswers();
    }

    [Fact]
    public async Task ASessionLookup_SearchesAndPicksDuringTheCall_AndRunsTheStep()
    {
        SessionLookupForms(Accepted(), Accepted());
        PatientsFound(Ava);

        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId, note = "x" }), LookupCatalog, Cancel));

        Assert.Equal(["lookup-search|patient", "lookup-pick|patient"], sessionForms.Select(x => x.Message));
        var (_, step, submitted, processId, _) = Assert.Single(posts);
        Assert.Equal("PickPatientStep", step);
        Assert.Equal(ProcessId, processId);
        Assert.Equal("P-1", submitted["patientId"]?.GetValue<string>());
        Assert.Equal("E-1", submitted["enrollmentId"]?.GetValue<string>());
        Assert.Equal("x", submitted["note"]?.GetValue<string>());
        Assert.Equal("completed", body["outcome"]?.GetValue<string>());
    }

    [Theory]
    [InlineData(false, "The user did not search (decline). Nothing was run.")]
    [InlineData(true, "The user did not search (accept). Nothing was run.")]
    public async Task ASessionLookup_WithoutASearch_RunsNothing(bool acceptedEmpty, string expected)
    {
        SessionLookupForms(acceptedEmpty ? Accepted(withContent: false) : Declined());

        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel));

        Assert.Equal($"{expected} clinic_pick_patient needs patientId and enrollmentId from one Patient Search result.", body["note"]?.GetValue<string>());
        Assert.Empty(queries);
        Assert.Empty(posts);
    }

    [Fact]
    public async Task ASessionLookup_WithAnEmptySearchText_RunsNothing()
    {
        SessionLookupForms(Accepted());
        searchAnswer = string.Empty;

        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel));

        Assert.StartsWith("The user did not search (accept).", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(queries);
    }

    [Fact]
    public async Task ASessionLookup_WithoutResults_SearchesAgain_WithANote()
    {
        SessionLookupForms(Accepted(), Declined());
        PatientsFound();

        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel));

        Assert.Equal(2, forms.Count);
        Assert.StartsWith("No Patient Search result matched \"Ava\". Try other details. ", forms[1].Items[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("The user did not search (decline).", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASessionLookup_TheUserDoesNotPick_RunsNothing()
    {
        SessionLookupForms(Accepted(), Declined());
        PatientsFound(Ava);

        var body = Body(await CreateSut().InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel));

        Assert.StartsWith("The user did not pick a result (decline). Nothing was run.", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(posts);
    }

    [Theory]
    [InlineData("search-again")]
    [InlineData("1")]
    public async Task ASessionLookup_SearchingAgainUntilTheRoundsRunOut_RunsNothing(string choice)
    {
        SessionLookupForms(Accepted(), Accepted(), Accepted(), Accepted(withContent: false));
        PatientsFound(Ava);
        pickAnswer = choice;

        var body = Body(await CreateSut(maxElicitationRounds: 2).InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel));

        Assert.Equal(4, sessionForms.Count);
        Assert.Equal("Search Patient Search: enter details that identify the record (e.g. Name).", forms[2].Items[0].Text);
        Assert.StartsWith("The user did not find the record. Nothing was run.", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(posts);
    }

    [Fact]
    public async Task ASessionLookup_WithoutAnyRoundsAllowed_RunsNothing()
    {
        SessionLookupForms();

        var body = Body(await CreateSut(maxElicitationRounds: 0).InvokeAsync(PickPatient, Call(new { processId = ProcessId }), LookupCatalog, Cancel));

        Assert.StartsWith("The user did not find the record.", body["note"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(sessionForms);
    }
}
