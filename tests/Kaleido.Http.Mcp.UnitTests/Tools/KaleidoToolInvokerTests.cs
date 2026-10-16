using System.Net;
using Kaleido.Http.Client;
using Kaleido.Processor;
using Kaleido.Queryable.Query;
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
    private readonly List<(string Service, string Step, JsonNode Body, Guid? ProcessId)> posts = [];
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

    private KaleidoToolInvoker CreateSut() =>
        new(
            processors.Object,
            queryables.Object,
            processForwarding.Object,
            elicitor.Object,
            new KaleidoMcpOptions
            {
                Instructions = "test",
                MaxElicitationRounds = 5
            },
            NullLogger<KaleidoToolInvoker>.Instance);

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
            client.Setup(x => x.ExecuteStepAsync(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Callback((string step, JsonElement body, Guid? processId, CancellationToken _) =>
                    posts.Add((service, step, JsonNode.Parse(body.GetRawText()) ?? new JsonObject(), processId)))
                .ReturnsAsync(response);
        }
    }

    private void StepThrows(Exception exception)
    {
        foreach (var client in services.Values)
        {
            client.Setup(x => x.ExecuteStepAsync(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
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

    private static readonly KaleidoTool ScopedSearch = Search with
    {
        Definition = new Tool
        {
            Name = "clinic_scoped_patient",
            InputSchema = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new { processId = new { type = "string", description = "The visit this search is for." } },
                required = new[] { "processId" }
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

        var (service, step, body, processId) = Assert.Single(posts);
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

        var (_, step, body, processId) = Assert.Single(posts);
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

    private async Task<string> MrtrStateAsync()
    {
        ClientShowsForms();
        server.Setup(x => x.IsMrtrSupported).Returns(true);
        StepReturns(StepResponse(required: QuestionsRequired));

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

        var (service, step, submitted, processId) = Assert.Single(posts);
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

        var (service, step, body, processId) = Assert.Single(posts);
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
}
