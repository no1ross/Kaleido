using Kaleido.Http.Client;
using Kaleido.Http.Mcp.Observability;
using Kaleido.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class KaleidoMcpToolHandlerTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IToolCatalogProvider> catalogs = new();
    private readonly Mock<IKaleidoToolInvoker> invoker = new();
    private readonly Mock<IMcpObservability> observability = new();
    private readonly Mock<IMcpToolCallObservation> callObservation = new();
    private readonly Mock<IMcpToolsListObservation> listObservation = new();
    private readonly Mock<McpServer> server = new();
    private readonly Mock<IKaleidoCallerForwarding> forwarding = new();
    private readonly Mock<IDisposable> forwardingScope = new();
    private string? inboundAuthorization = "Bearer carol-token";

    private static readonly KaleidoTool Search = new()
    {
        Definition = new Tool { Name = "clinic_patient" },
        Kind = KaleidoToolKind.Query,
        Service = "clinic",
        Url = "/clinic/queryable/patients/query"
    };

    public KaleidoMcpToolHandlerTests()
    {
        observability.Setup(x => x.BeginToolCall(It.IsAny<string>())).Returns(callObservation.Object);
        observability.Setup(x => x.BeginToolsList()).Returns(listObservation.Object);
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ToolCatalog { Tools = [Search] });
        forwarding.Setup(x => x.ForwardCaller(It.IsAny<string?>())).Returns(forwardingScope.Object);
    }

    private KaleidoMcpToolHandler CreateSut(ILogger<KaleidoMcpToolHandler>? logger = null, bool inAnHttpRequest = true)
    {
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = "req-7" });

        var context = new DefaultHttpContext();

        if (inboundAuthorization is not null)
        {
            context.Request.Headers.Authorization = inboundAuthorization;
        }

        var httpContext = new Mock<IHttpContextAccessor>();
        httpContext.Setup(x => x.HttpContext).Returns(inAnHttpRequest ? context : null);

        return new KaleidoMcpToolHandler(
            catalogs.Object,
            invoker.Object,
            observability.Object,
            correlation.Object,
            forwarding.Object,
            httpContext.Object,
            logger ?? NullLogger<KaleidoMcpToolHandler>.Instance);
    }

    private sealed class RecordingLogger : ILogger<KaleidoMcpToolHandler>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Fact]
    public async Task ListTools_WhenReadingFailsUnexpectedly_LogsTheFailure_AtError()
    {
        var failure = new InvalidCastException("boom");
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var logger = new RecordingLogger();

        await CreateSut(logger).ListToolsAsync(ListRequest(), Cancel);
        await CreateSut(logger).ListToolsAsync(ListRequest(), Cancel);

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, x =>
        {
            Assert.Equal(LogLevel.Error, x.Level);
            Assert.Equal("Kaleido MCP: listing tools failed unexpectedly; returning no tools.", x.Message);
            Assert.Same(failure, x.Exception);
        });
        listObservation.Verify(x => x.Completed(It.IsAny<ToolCatalog>()), Times.Never);
    }

    [Fact]
    public async Task ListTools_WhenTheCallerCancels_Propagates()
    {
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().ListToolsAsync(ListRequest(), Cancel).AsTask());

        listObservation.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task CallTool_WhenReadingTheToolsFailsUnexpectedly_LogsTheFailure_ForTheTool()
    {
        var failure = new InvalidCastException("boom");
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var logger = new RecordingLogger();

        await CreateSut(logger).CallToolAsync(CallRequest("clinic_patient"), Cancel);
        await CreateSut(logger).CallToolAsync(CallRequest("clinic_chart"), Cancel);

        Assert.Equal(
            [
                (LogLevel.Error, "Kaleido MCP: reading the tools failed unexpectedly for clinic_patient.", (Exception?)failure),
                (LogLevel.Error, "Kaleido MCP: reading the tools failed unexpectedly for clinic_chart.", failure)
            ],
            logger.Entries);
        invoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CallTool_WhenReadingTheToolsIsCanceled_IsObservedAsCanceled_AndPropagates()
    {
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel).AsTask());

        callObservation.Verify(x => x.Canceled());
        callObservation.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task CallTool_WithoutParams_IsAnUnknownToolResult_ForTheEmptyName()
    {
        var request = new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, parameters: null!);

        var result = await CreateSut().CallToolAsync(request, Cancel);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.UnknownTool, Body(result)["code"]?.GetValue<string>());
        observability.Verify(x => x.BeginToolCall(string.Empty));
        invoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CallTool_WithoutParams_RunsTheToolOfTheEmptyName_WithNoArgumentsOrState()
    {
        var unnamed = Search with { Definition = new Tool { Name = string.Empty } };
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ToolCatalog { Tools = [unnamed] });
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(unnamed, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("{}"));
        var request = new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, parameters: null!);

        await CreateSut().CallToolAsync(request, Cancel);

        Assert.NotNull(call);
        Assert.Empty(call.Arguments);
        Assert.Null(call.Meta);
        Assert.Null(call.InputResponses);
        Assert.Null(call.RequestState);
    }

    [Fact]
    public async Task CallTool_WithoutAnyRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().CallToolAsync(null!, Cancel).AsTask());
    }

    [Fact]
    public async Task CallTool_WhenTheToolReturnsNothing_IsAnInternalErrorResult_WithTheRequestId()
    {
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolOutcome());

        var result = await CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.InternalError, Body(result)["code"]?.GetValue<string>());
        Assert.Equal("clinic_patient returned no result.", Body(result)["message"]?.GetValue<string>());
        Assert.Equal("req-7", Body(result)["requestId"]?.GetValue<string>());
        callObservation.Verify(x => x.Completed(result));
    }

    [Fact]
    public async Task CallTool_WithoutArguments_RunsTheToolWithNone()
    {
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("{}"));

        await CreateSut().CallToolAsync(
            new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams { Name = "clinic_patient" }),
            Cancel);

        Assert.NotNull(call);
        Assert.Empty(call.Arguments);
        Assert.Null(call.Meta);
        Assert.Null(call.InputResponses);
        Assert.Null(call.RequestState);
        callObservation.Verify(x => x.Resolved(Search, null));
    }

    [Fact]
    public async Task CallTool_PassesTheMetaAndTheMrtrAnswersThrough()
    {
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("{}"));
        var meta = new JsonObject { ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject() };
        var answers = new InputResponse { RawValue = JsonSerializer.SerializeToElement(new { action = "accept" }) };

        await CreateSut().CallToolAsync(
            new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams
            {
                Name = "clinic_patient",
                Meta = meta,
                InputResponses = new Dictionary<string, InputResponse> { ["kaleido-answers"] = answers }
            }),
            Cancel);

        Assert.Same(meta, call?.Meta);
        Assert.Same(answers, Assert.Single(call?.InputResponses ?? new Dictionary<string, InputResponse>(), x => x.Key == "kaleido-answers").Value);
    }

    [Fact]
    public async Task CallTool_OutsideAnHttpRequest_IsForwardedAsAnonymous()
    {
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Text("{}"));

        await CreateSut(inAnHttpRequest: false).CallToolAsync(CallRequest("clinic_patient"), Cancel);

        forwarding.Verify(x => x.ForwardCaller(null), Times.Once);
        forwardingScope.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task CallTool_LeavesTextThatOnlyLooksLikeJsonAsItIs_AndStampsTheJsonBlocks()
    {
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallToolResult
            {
                Content =
                [
                    new TextContentBlock { Text = "{not json" },
                    new TextContentBlock { Text = """{"results":[]}""" },
                    new TextContentBlock { Text = "{}" }
                ]
            });

        var result = await CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel);

        var texts = result.Content.OfType<TextContentBlock>().Select(x => x.Text).ToArray();
        Assert.Equal("{not json", texts[0]);
        Assert.Equal("req-7", JsonNode.Parse(texts[1])?["requestId"]?.GetValue<string>());
        Assert.Equal("req-7", JsonNode.Parse(texts[2])?["requestId"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"name":7}""")]
    [InlineData("""{"name":null}""")]
    public async Task CallTool_WithANameThatIsNotText_IsAnUnknownToolResult(string arguments)
    {
        FrontDoorCatalog();

        var result = await CreateSut().CallToolAsync(
            new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams
            {
                Name = ToolCatalogBuilder.CallToolTool,
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments)
            }),
            Cancel);

        Assert.Equal(McpErrorCodes.UnknownTool, Body(result)["code"]?.GetValue<string>());
        invoker.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("""{"name":"clinic_close_visit"}""")]
    [InlineData("""{"name":"clinic_close_visit","arguments":"processId=1"}""")]
    [InlineData("""{"name":"clinic_close_visit","arguments":null}""")]
    public async Task CallTool_WithoutAnArgumentsObject_RunsTheNamedToolWithNone(string arguments)
    {
        FrontDoorCatalog();
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(HiddenStep, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("{}"));

        await CreateSut().CallToolAsync(
            new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams
            {
                Name = ToolCatalogBuilder.CallToolTool,
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments)
            }),
            Cancel);

        Assert.NotNull(call);
        Assert.Empty(call.Arguments);
        callObservation.Verify(x => x.Resolved(HiddenStep, null));
    }

    [Theory]
    [InlineData("""{"processId":42}""")]
    [InlineData("""{"processId":"not-a-guid"}""")]
    public async Task CallTool_WithAProcessIdThatIsNotAnId_DoesNotTagIt_AndLeavesItToTheTool(string arguments)
    {
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("{}"));

        await CreateSut().CallToolAsync(
            new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams
            {
                Name = "clinic_patient",
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments)
            }),
            Cancel);

        callObservation.Verify(x => x.Resolved(Search, null));
        Assert.True(call?.Arguments.ContainsKey("processId"));
    }

    private RequestContext<CallToolRequestParams> CallRequest(string tool, string? requestState = null) =>
        new(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams
        {
            Name = tool,
            Arguments = new Dictionary<string, JsonElement> { ["searchText"] = JsonSerializer.SerializeToElement("Ava") },
            RequestState = requestState
        });

    private RequestContext<ListToolsRequestParams> ListRequest() =>
        new(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsList }, new ListToolsRequestParams());

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)!;

    private static CallToolResult Text(string json) =>
        new() { Content = [new TextContentBlock { Text = json }] };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListTools_ReturnsTheCallersTools_AndObservesTheListing()
    {
        var result = await CreateSut().ListToolsAsync(ListRequest(), Cancel);

        Assert.Equal(["clinic_patient"], result.Tools.Select(x => x.Name));
        listObservation.Verify(x => x.Completed(It.Is<ToolCatalog>(c => c.Tools.Count == 1)));
    }

    private static readonly KaleidoTool HiddenStep = new()
    {
        Definition = new Tool { Name = "clinic_close_visit" },
        Kind = KaleidoToolKind.Step,
        Service = "clinic",
        Listed = false
    };

    private static readonly KaleidoTool CallTool = new()
    {
        Definition = new Tool { Name = ToolCatalogBuilder.CallToolTool },
        Kind = KaleidoToolKind.CallTool
    };

    private void FrontDoorCatalog() =>
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCatalog { Tools = [Search, HiddenStep, CallTool], IsFrontDoor = true });

    private RequestContext<CallToolRequestParams> CallThrough(string? name, object? arguments = null) =>
        new(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams
        {
            Name = ToolCatalogBuilder.CallToolTool,
            Arguments = name is null
                ? new Dictionary<string, JsonElement>()
                : new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement(name),
                    ["arguments"] = JsonSerializer.SerializeToElement(arguments ?? new { })
                }
        });

    [Fact]
    public async Task ListTools_ReturnsOnlyTheListedTools()
    {
        FrontDoorCatalog();

        var result = await CreateSut().ListToolsAsync(ListRequest(), Cancel);

        Assert.Equal(["clinic_patient", ToolCatalogBuilder.CallToolTool], result.Tools.Select(x => x.Name));
    }

    [Fact]
    public async Task CallTool_RunsTheNamedTool_WithItsArguments_AsIfItWereCalled()
    {
        FrontDoorCatalog();
        var processId = Guid.NewGuid();
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(HiddenStep, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("{}"));

        await CreateSut().CallToolAsync(CallThrough("clinic_close_visit", new { processId = processId.ToString(), note = "done" }), Cancel);

        Assert.Equal("done", call?.Arguments["note"].GetString());
        callObservation.Verify(x => x.Resolved(HiddenStep, processId.ToString()));
        forwarding.Verify(x => x.ForwardCaller("Bearer carol-token"), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("clinic_chart")]
    [InlineData(ToolCatalogBuilder.CallToolTool)]
    public async Task CallTool_WithoutAToolOfTheCaller_IsAnUnknownToolResult(string? name)
    {
        FrontDoorCatalog();

        var result = await CreateSut().CallToolAsync(CallThrough(name), Cancel);

        Assert.Equal(McpErrorCodes.UnknownTool, Body(result)["code"]?.GetValue<string>());
        invoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListTools_WhenReadingFailsUnexpectedly_ReturnsNoTools()
    {
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidCastException());

        var result = await CreateSut().ListToolsAsync(ListRequest(), Cancel);

        Assert.Empty(result.Tools);
    }

    [Fact]
    public async Task CallTool_RunsTheTool_WithTheCallsArguments_AndAddsTheRequestId()
    {
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("""{"results":[]}"""));

        var result = await CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel);

        Assert.Equal("Ava", call?.Arguments["searchText"].GetString());
        Assert.Same(server.Object, call?.Server);
        Assert.Equal("req-7", Body(result)["requestId"]?.GetValue<string>());
        callObservation.Verify(x => x.Resolved(Search, null));
        callObservation.Verify(x => x.Completed(result));
    }

    [Fact]
    public async Task CallTool_TagsTheCall_WithItsProcessIdArgument()
    {
        var processId = Guid.NewGuid();
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Text("{}"));

        await CreateSut().CallToolAsync(
            new RequestContext<CallToolRequestParams>(server.Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams
            {
                Name = "clinic_patient",
                Arguments = new Dictionary<string, JsonElement> { ["processId"] = JsonSerializer.SerializeToElement(processId.ToString()) }
            }),
            Cancel);

        callObservation.Verify(x => x.Resolved(Search, processId.ToString()));
    }

    [Fact]
    public async Task CallTool_PassesTheMrtrStateThrough()
    {
        KaleidoToolCall? call = null;
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback((KaleidoTool _, KaleidoToolCall given, ToolCatalog _, CancellationToken _) => call = given)
            .ReturnsAsync(Text("{}"));

        await CreateSut().CallToolAsync(CallRequest("clinic_patient", requestState: "kaleido-mcp:abc"), Cancel);

        Assert.Equal("kaleido-mcp:abc", call?.RequestState);
    }

    [Fact]
    public async Task CallTool_ForAToolTheCallerDoesNotHave_IsAnUnknownToolResult()
    {
        var result = await CreateSut().CallToolAsync(CallRequest("clinic_chart"), Cancel);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.UnknownTool, Body(result)["code"]?.GetValue<string>());
        Assert.Equal("req-7", Body(result)["requestId"]?.GetValue<string>());
        invoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CallTool_WhenKaleidoIsUnavailable_IsAServiceUnavailableResult()
    {
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ToolCatalog { Unavailable = "registry down" });

        var result = await CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel);

        Assert.Equal(McpErrorCodes.ServiceUnavailable, Body(result)["code"]?.GetValue<string>());
        Assert.Contains("registry down", Body(result)["message"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallTool_WhenReadingTheToolsFailsUnexpectedly_IsAnInternalErrorResult()
    {
        catalogs.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidCastException());

        var result = await CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel);

        Assert.Equal(McpErrorCodes.InternalError, Body(result)["code"]?.GetValue<string>());
    }

    [Fact]
    public async Task CallTool_InputRequired_IsSignalledToTheSdk_AndObserved()
    {
        var inputRequired = new InputRequiredResult { RequestState = "kaleido-mcp:x" };
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolOutcome { InputRequired = inputRequired });

        var exception = await Assert.ThrowsAsync<InputRequiredException>(() => CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel).AsTask());

        Assert.Same(inputRequired, exception.Result);
        callObservation.Verify(x => x.InputRequired());
    }

    [Fact]
    public async Task CallTool_Cancellation_IsObservedAsCanceled_AndPropagates()
    {
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel).AsTask());

        callObservation.Verify(x => x.Canceled());
        callObservation.Verify(x => x.Completed(It.IsAny<CallToolResult>()), Times.Never);
    }

    [Fact]
    public async Task CallTool_LeavesNonJsonTextAsItIs()
    {
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Text("plain text"));

        var result = await CreateSut().CallToolAsync(CallRequest("clinic_patient"), Cancel);

        Assert.Equal("plain text", result.Content.OfType<TextContentBlock>().First().Text);
    }

    [Fact]
    public async Task ATool_RunsAsTheCaller_InsideACallerForwardingScope()
    {
        var forwardedDuringTheCall = false;
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .Callback(() => forwardedDuringTheCall = forwarding.Invocations.Any(i => i.Method.Name == nameof(IKaleidoCallerForwarding.ForwardCaller)))
            .ReturnsAsync(new ToolOutcome { Result = new CallToolResult { Content = [new TextContentBlock { Text = "{}" }] } });

        await CreateSut().CallToolAsync(CallRequest("clinic_patient"), TestContext.Current.CancellationToken);

        Assert.True(forwardedDuringTheCall);
        forwarding.Verify(x => x.ForwardCaller("Bearer carol-token"), Times.Once);
        forwardingScope.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task AnAnonymousCaller_IsForwardedAsAnonymous()
    {
        inboundAuthorization = null;
        invoker.Setup(x => x.InvokeAsync(Search, It.IsAny<KaleidoToolCall>(), It.IsAny<ToolCatalog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolOutcome { Result = new CallToolResult { Content = [new TextContentBlock { Text = "{}" }] } });

        await CreateSut().CallToolAsync(CallRequest("clinic_patient"), TestContext.Current.CancellationToken);

        forwarding.Verify(x => x.ForwardCaller(It.Is<string?>(v => string.IsNullOrEmpty(v))), Times.Once);
    }

    [Fact]
    public async Task ListingTools_DoesNotForwardTheCaller()
    {
        await CreateSut().ListToolsAsync(ListRequest(), TestContext.Current.CancellationToken);

        forwarding.Verify(x => x.ForwardCaller(It.IsAny<string?>()), Times.Never);
    }
}
