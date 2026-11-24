using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Testing.Identity;
using Kaleido.Process.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.FunctionalTests;

public sealed class ProcessHttpServerFunctionalTests
{
    private const string StepUrl = "/catalog/1/processes/steps/revieworderstep";

    [Fact]
    public async Task LocalProcess_ExecutesAndReadsNeutralStateWithoutARegistryParticipant()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var requestId = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, requestId);
        using var execute = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);
        var result = await execute.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var process = Assert.IsType<ProcessResult>(result);
        Assert.Equal("1", process.ServiceVersion);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(process.Steps).ExecutionStatus);
        Assert.Equal(process.ProcessId.ToString(), execute.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId).Single());
        Assert.Equal(requestId, execute.Headers.GetValues(KaleidoCorrelationHeaders.RequestId).Single());

        using var stateResponse = await client.GetAsync($"/catalog/1/processes/{process.ProcessId:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var snapshot = await stateResponse.Content.ReadFromJsonAsync<ProcessStateSnapshot>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal((process.ProcessId, "catalog", "1"), (Assert.IsType<ProcessStateSnapshot>(snapshot).ProcessId, snapshot.ServiceName, snapshot.ServiceVersion));
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(snapshot.Steps, step => step.StepName == nameof(ReviewOrderStep)).Status);
        Assert.DoesNotContain("executeUrl", await stateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task AnAuthenticatedCaller_KeepsItsInboundRequestId()
    {
        await using var app = await StartAsync(authorizationMode: KaleidoAuthorizationMode.None);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/catalog/1/processes/{Guid.Empty:D}");
        request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reviewer"]));
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, "req-42");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("req-42", Assert.Single(response.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
    }

    [Fact]
    public async Task AnAnonymousCaller_GetsAGeneratedRequestIdInsteadOfTheOneItSent()
    {
        await using var app = await StartAsync(authorizationMode: KaleidoAuthorizationMode.None);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/catalog/1/processes/{Guid.Empty:D}");
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, "req-42");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var echoed = Assert.Single(response.Headers.GetValues(KaleidoCorrelationHeaders.RequestId));
        Assert.NotEqual("req-42", echoed);
        Assert.True(Guid.TryParse(echoed, out _));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-audience")]
    [InlineData("forged")]
    public async Task AStep_RejectsMissingOrInvalidBearerTokens(string tokenKind)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        using var request = new HttpRequestMessage(HttpMethod.Post, StepUrl)
        {
            Content = JsonContent.Create(new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } }, options: KaleidoJsonOptions.Options),
        };
        var token = tokenKind switch
        {
            "missing" => null,
            "wrong-audience" => TestTokenIssuer.User("bob", ["reviewer"], audience: "another-api"),
            "forged" => TestTokenIssuer.Forged("bob", ["reviewer"]),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenKind)),
        };
        if (token is not null)
        {
            request.Headers.Authorization = TestJwtAuthentication.Bearer(token);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task AnAppOnlyBearerToken_UsesItsRolesOnTypedAndGenericRoutes()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("intake", ["reviewer"]));
        using var response = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest
            {
                StepName = nameof(ReviewOrderStep),
                Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = "A-2" }, KaleidoJsonOptions.Options),
            }],
        };
        using var generic = await client.PostAsJsonAsync("/catalog/1/processes/execute", body,
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, generic.StatusCode);
        Assert.Equal(2, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task GenericExecute_UsesTheBearerTokensStepRole()
    {
        await using var app = await StartAsync();
        using var reviewer = Client(app);
        using var viewer = Client(app, roles: ["viewer"]);
        var body = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest
            {
                StepName = nameof(ReviewOrderStep),
                Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = "A-1" }, KaleidoJsonOptions.Options),
            }],
        };

        using var allowed = await reviewer.PostAsJsonAsync("/catalog/1/processes/execute", body, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var denied = await viewer.PostAsJsonAsync("/catalog/1/processes/execute", body, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Theory]
    [InlineData("reviewer", HttpStatusCode.OK)]
    [InlineData("REVIEWER", HttpStatusCode.Forbidden)]
    [InlineData("Reviewer", HttpStatusCode.Forbidden)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    public async Task ADeclaredRole_MatchesExactlyOnTypedAndGenericRoutes(string role, HttpStatusCode expected)
    {
        await using var app = await StartAsync();
        using var client = Client(app, roles: [role]);
        var step = new ReviewOrderStep { Code = "A-1" };
        using var typed = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = step },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var body = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest { StepName = nameof(ReviewOrderStep), Request = JsonSerializer.SerializeToElement(step, KaleidoJsonOptions.Options) }],
        };
        using var generic = await client.PostAsJsonAsync("/catalog/1/processes/execute", body, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(expected, typed.StatusCode);
        Assert.Equal(expected, generic.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
        {
            Assert.Empty(await typed.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(expected == HttpStatusCode.OK ? 2 : 0, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task NamedHostPolicy_AuthorizesTypedAndGenericBearerCalls()
    {
        await using var app = await StartAsync(includePolicyStep: true);
        using var clinician = Client(app, roles: ["clinician"]);
        using var reviewer = Client(app);
        var body = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest
            {
                StepName = nameof(ClinicianStep),
                Request = JsonSerializer.SerializeToElement(new ClinicianStep(), KaleidoJsonOptions.Options),
            }],
        };

        using var typedAllowed = await clinician.PostAsJsonAsync("/catalog/1/processes/steps/clinicianstep",
            new ExecuteStepRequest<ClinicianStep> { ProcessStep = new ClinicianStep() }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var genericAllowed = await clinician.PostAsJsonAsync("/catalog/1/processes/execute", body, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var typedDenied = await reviewer.PostAsJsonAsync("/catalog/1/processes/steps/clinicianstep",
            new ExecuteStepRequest<ClinicianStep> { ProcessStep = new ClinicianStep() }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var genericDenied = await reviewer.PostAsJsonAsync("/catalog/1/processes/execute", body, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, typedAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, genericAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, typedDenied.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, genericDenied.StatusCode);
    }

    [Fact]
    public async Task HostsOwnSuperuserRoleHandler_AdmitsTypedAndGenericCalls()
    {
        await using var app = await StartAsync(superuserHandler: true);
        using var client = Client(app, roles: ["superuser"]);
        var body = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest
            {
                StepName = nameof(ReviewOrderStep),
                Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = "A-2" }, KaleidoJsonOptions.Options),
            }],
        };

        using var typed = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var generic = await client.PostAsJsonAsync("/catalog/1/processes/execute", body,
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, typed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, generic.StatusCode);
        Assert.Equal(2, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task AnonymousStep_IsCallableThroughTypedAndGenericRoutes_ButCannotBypassASecuredStep()
    {
        await using var app = await StartAsync(includeAnonymousStep: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        using var typed = await client.PostAsJsonAsync("/catalog/1/processes/steps/publicreviewstep",
            new ExecuteStepRequest<PublicReviewStep> { ProcessStep = new PublicReviewStep() },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, typed.StatusCode);
        var publicStep = new ProcessStepRequest { StepName = nameof(PublicReviewStep), Request = JsonSerializer.SerializeToElement(new PublicReviewStep(), KaleidoJsonOptions.Options) };
        using var generic = await client.PostAsJsonAsync("/catalog/1/processes/execute",
            new ExecuteProcessRequest { Steps = [publicStep] }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, generic.StatusCode);
        Assert.Equal(2, app.Services.GetRequiredService<AnonymousRuns>().Count);

        var securedStep = new ProcessStepRequest { StepName = nameof(ReviewOrderStep), Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = "A-1" }, KaleidoJsonOptions.Options) };
        using var mixed = await client.PostAsJsonAsync("/catalog/1/processes/execute",
            new ExecuteProcessRequest { Steps = [publicStep, securedStep] }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, mixed.StatusCode);
        Assert.Equal(2, app.Services.GetRequiredService<AnonymousRuns>().Count);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);

        using var unknown = await client.PostAsJsonAsync("/catalog/1/processes/execute",
            new ExecuteProcessRequest { Steps = [new ProcessStepRequest { StepName = "NoSuchStep", Request = JsonSerializer.SerializeToElement(new { }, KaleidoJsonOptions.Options) }] },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task AnotherAuthenticatedCaller_CanReadAndContinueAProcess_WhenTheNextStepAllowsIt()
    {
        await using var app = await StartAsync(includeOpenStep: true);
        using var alice = Client(app, user: "alice");
        using var bob = Client(app, roles: ["viewer"], user: "bob");
        using var carol = Client(app, roles: [], user: "carol");
        using var created = await alice.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var processId = Assert.IsType<ProcessResult>(await created.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).ProcessId;
        var url = $"/catalog/1/processes/{processId:D}";

        using var before = await bob.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var resume = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/steps/openfollowupstep")
        {
            Content = JsonContent.Create(new ExecuteStepRequest<OpenFollowUpStep> { ProcessStep = new OpenFollowUpStep() }, options: KaleidoJsonOptions.Options),
        };
        resume.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
        using var continued = await bob.SendAsync(resume, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, continued.StatusCode);

        using var stateResponse = await carol.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var state = Assert.IsType<ProcessStateSnapshot>(await stateResponse.Content.ReadFromJsonAsync<ProcessStateSnapshot>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(("alice", "user-alice"), (state.CreatedBy?.Name, state.CreatedBy?.Id));
        Assert.Equal(("bob", "user-bob"), (state.UpdatedBy?.Name, state.UpdatedBy?.Id));
        var reviewed = Assert.Single(state.Steps, step => step.StepName == nameof(ReviewOrderStep));
        var followedUp = Assert.Single(state.Steps, step => step.StepName == nameof(OpenFollowUpStep));
        Assert.Equal("alice", reviewed.StartedBy?.Name);
        Assert.Equal("alice", reviewed.LastUpdatedBy?.Name);
        Assert.NotNull(reviewed.FirstExecuted);
        Assert.Equal("bob", followedUp.StartedBy?.Name);
        Assert.Equal("bob", followedUp.LastUpdatedBy?.Name);

        using var refused = new HttpRequestMessage(HttpMethod.Post, StepUrl)
        {
            Content = JsonContent.Create(new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-2" } }, options: KaleidoJsonOptions.Options),
        };
        refused.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
        using var denied = await bob.SendAsync(refused, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task AService_MapsOnlyItsVersionedStepRoute()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var request = new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } };

        using var versioned = await client.PostAsJsonAsync(StepUrl, request, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var unversioned = await client.PostAsJsonAsync("/catalog/processes/steps/revieworderstep", request, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unversioned.StatusCode);
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task Escalation_HidesAnAdminRequiredStepFromTheClerk_ButShowsItToTheAdmin()
    {
        await using var app = await StartAsync(includeVisibility: true);
        using var clerk = Client(app, roles: ["vis-clerk"]);
        using var admin = Client(app, roles: ["vis-admin"]);
        using var startedResponse = await clerk.PostAsJsonAsync("/catalog/1/processes/steps/visibilitystartstep",
            new ExecuteStepRequest<VisibilityStartStep> { ProcessStep = new VisibilityStartStep() },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, startedResponse.StatusCode);
        var started = Assert.IsType<ProcessResult>(await startedResponse.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal([nameof(VisibilityClerkStep), nameof(VisibilityEscalateStep)], started.AvailableSteps.Order(StringComparer.Ordinal));
        Assert.Null(started.Notice);

        using var escalate = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/steps/visibilityescalatestep")
        {
            Content = JsonContent.Create(new ExecuteStepRequest<VisibilityEscalateStep> { ProcessStep = new VisibilityEscalateStep() }, options: KaleidoJsonOptions.Options),
        };
        escalate.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, started.ProcessId.ToString("D"));
        using var escalatedResponse = await clerk.SendAsync(escalate, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, escalatedResponse.StatusCode);
        var escalated = Assert.IsType<ProcessResult>(await escalatedResponse.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Null(escalated.RequiredStep);
        Assert.Empty(escalated.AvailableSteps);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, escalated.Notice?.Code);

        var url = $"/catalog/1/processes/{started.ProcessId:D}";
        var clerkState = Assert.IsType<ProcessStateSnapshot>(await clerk.GetFromJsonAsync<ProcessStateSnapshot>(url, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Null(clerkState.RequiredStep);
        Assert.Empty(clerkState.AvailableSteps);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, clerkState.Notice?.Code);
        var adminState = Assert.IsType<ProcessStateSnapshot>(await admin.GetFromJsonAsync<ProcessStateSnapshot>(url, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(nameof(VisibilityAdminStep), adminState.RequiredStep);
    }

    [Fact]
    public async Task ContinuationNames_AreFilteredForTheCallerOnExecuteAndState()
    {
        await using var app = await StartAsync();
        using var reviewer = Client(app);
        using var execute = await reviewer.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);
        var result = Assert.IsType<ProcessResult>(await execute.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Empty(result.AvailableSteps);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, result.Notice?.Code);

        var reviewerState = await reviewer.GetFromJsonAsync<ProcessStateSnapshot>($"/catalog/1/processes/{result.ProcessId:D}", KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var filtered = Assert.IsType<ProcessStateSnapshot>(reviewerState);
        Assert.Empty(filtered.AvailableSteps);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, filtered.Notice?.Code);

        using var admin = Client(app, roles: ["reviewer", "admin"]);
        var adminState = await admin.GetFromJsonAsync<ProcessStateSnapshot>($"/catalog/1/processes/{result.ProcessId:D}", KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var visible = Assert.IsType<ProcessStateSnapshot>(adminState);
        Assert.Equal([nameof(AdminOnlyStep)], visible.AvailableSteps);
        Assert.Null(visible.Notice);
    }

    [Fact]
    public async Task HandOff_ReturnsAndPersistsTheTargetVersionWithoutResolvingAUrl()
    {
        await using var app = await StartAsync(handOff: true);
        using var client = Client(app);
        using var execute = await client.PostAsJsonAsync("/catalog/1/processes/steps/handoffstep",
            new ExecuteStepRequest<HandOffStep> { ProcessStep = new HandOffStep() },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);
        var response = Assert.IsType<ProcessResult>(await execute.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(("1", "target", "2"), (response.ServiceVersion, response.TargetProcessorName, response.TargetProcessorVersion));
        Assert.Equal(["FinishStep"], response.AvailableSteps);
        Assert.DoesNotContain("executeUrl", await execute.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.OrdinalIgnoreCase);

        using var stateResponse = await client.GetAsync($"/catalog/1/processes/{response.ProcessId:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var snapshot = Assert.IsType<ProcessStateSnapshot>(await stateResponse.Content.ReadFromJsonAsync<ProcessStateSnapshot>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(("1", "target", "2"), (snapshot.ServiceVersion, snapshot.TargetProcessorName, snapshot.TargetProcessorVersion));
    }

    [Fact]
    public async Task AHandlerCannotHandOffToItsOwnService_AndTheProcessCanStillContinue()
    {
        await using var app = await StartAsync(handOff: true, includeFrameworkMessages: true);
        using var client = Client(app);
        using var invalid = await client.PostAsJsonAsync("/catalog/1/processes/steps/selfhandoffstep",
            new ExecuteStepRequest<SelfHandOffStep> { ProcessStep = new SelfHandOffStep() },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var refused = Assert.IsType<ProcessResult>(await invalid.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        var failed = Assert.Single(refused.Steps);
        Assert.Equal(StepExecutionStatus.Exception, failed.ExecutionStatus);
        Assert.Contains(failed.RuntimeMessages, message => message.Code == ProcessorErrorCodes.InvalidHandOff);
        Assert.Null(refused.TargetProcessorName);
        var state = Assert.IsType<ProcessStateSnapshot>(await client.GetFromJsonAsync<ProcessStateSnapshot>($"/catalog/1/processes/{refused.ProcessId:D}", KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.NotEqual(ProcessExecutionState.HandOff, state.State);
        Assert.Null(state.TargetProcessorName);
        Assert.Contains(state.Steps, step => step.StepName == nameof(SelfHandOffStep) && step.Status == StepExecutionStatus.Exception);

        using var valid = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/steps/handoffstep")
        {
            Content = JsonContent.Create(new ExecuteStepRequest<HandOffStep> { ProcessStep = new HandOffStep() }, options: KaleidoJsonOptions.Options),
        };
        valid.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, refused.ProcessId.ToString("D"));
        using var response = await client.SendAsync(valid, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var continued = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(ProcessExecutionState.HandOff, continued.State);
        Assert.Equal("target", continued.TargetProcessorName);
    }

    [Fact]
    public async Task MultiStepExecute_ReplaysAKeyWithoutRunningTheStepAgain_AndRefusesAChangedBody()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        using var first = await SendAsync(client, "A-1", "same-operation");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var replay = await SendAsync(client, "A-1", "same-operation");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed).Single());
        var repeated = await replay.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(Assert.IsType<ProcessResult>(original).ProcessId, Assert.IsType<ProcessResult>(repeated).ProcessId);
        Assert.Equal(original.ProcessId.ToString(), replay.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId).Single());
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);

        using var changed = await SendAsync(client, "A-2", "same-operation");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        var error = await changed.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(ProcessorErrorCodes.IdempotencyKeyReused, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task OneIdempotencyKey_HasIndependentResultsAndReplaysForDifferentCallers()
    {
        await using var app = await StartAsync();
        using var alice = Client(app);
        using var bob = Client(app, user: "bob");
        var key = $"shared-{Guid.NewGuid():N}";

        using var first = await SendAsync(alice, "A-1", key);
        using var second = await SendAsync(bob, "A-1", key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var aliceResult = Assert.IsType<ProcessResult>(await first.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        var bobResult = Assert.IsType<ProcessResult>(await second.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.NotEqual(aliceResult.ProcessId, bobResult.ProcessId);

        using var aliceReplay = await SendAsync(alice, "A-1", key);
        using var bobReplay = await SendAsync(bob, "A-1", key);
        Assert.Equal(HttpStatusCode.OK, aliceReplay.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bobReplay.StatusCode);
        Assert.Equal("true", Assert.Single(aliceReplay.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));
        Assert.Equal("true", Assert.Single(bobReplay.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));
        Assert.Equal(aliceResult.ProcessId, Assert.IsType<ProcessResult>(await aliceReplay.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).ProcessId);
        Assert.Equal(bobResult.ProcessId, Assert.IsType<ProcessResult>(await bobReplay.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).ProcessId);
        Assert.Equal(2, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task IdempotencyScope_SeparatesCallerNamesAndKeysContainingASeparator()
    {
        await using var app = await StartAsync();
        using var alice = Client(app);
        using var other = Client(app, user: "alice:tenant");
        var suffix = Guid.NewGuid().ToString("N");

        using var first = await SendAsync(alice, "A-1", $"tenant:{suffix}");
        using var second = await SendAsync(other, "A-1", suffix);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstResult = Assert.IsType<ProcessResult>(await first.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        var secondResult = Assert.IsType<ProcessResult>(await second.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.NotEqual(firstResult.ProcessId, secondResult.ProcessId);
        Assert.Equal(2, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task ACorrectedKeyedRequest_CanRunAfterTheFirstRequestWasRefused()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/execute")
        {
            Content = JsonContent.Create(new ExecuteProcessRequest
            {
                Steps = [new ProcessStepRequest { StepName = "NoSuchStep", Request = JsonSerializer.SerializeToElement(new { }, KaleidoJsonOptions.Options) }],
            }, options: KaleidoJsonOptions.Options),
        };
        invalid.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, "correct-after-refusal");
        using var refused = await client.SendAsync(invalid, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        using var corrected = await SendAsync(client, "A-1", "correct-after-refusal");
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.False(corrected.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.Equal(1, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task LocalProcess_EnforcesTheStepRoleAndLeavesUnrelatedHostRoutesAlone()
    {
        await using var app = await StartAsync();
        using var client = Client(app, roles: []);
        using var forbidden = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var multi = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest
            {
                StepName = nameof(ReviewOrderStep),
                Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = "A-1" }, KaleidoJsonOptions.Options),
            }],
        };
        using var deniedMulti = await client.PostAsJsonAsync("/catalog/1/processes/execute", multi, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedMulti.StatusCode);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);

        using var ordinary = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ordinary.StatusCode);
        Assert.False(ordinary.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
    }

    [Fact]
    public async Task ConcurrentStateChange_AnswersAProcessConflictWithoutRunningTheStep()
    {
        await using var app = await StartAsync();
        app.Services.GetRequiredService<StoredProcesses>().RefuseNextSave();
        using var client = Client(app);

        using var response = await client.PostAsJsonAsync(StepUrl,
            new ExecuteStepRequest<ReviewOrderStep> { ProcessStep = new ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(ProcessorErrorCodes.ProcessConflict, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);
    }

    [Fact]
    public async Task InvalidStepInput_IsRejectedBeforeCreatingAProcess()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, StepUrl)
        {
            Content = new StringContent("""{"processStep":{}}""", System.Text.Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.NotEmpty(Assert.IsType<KaleidoErrorResponse>(error).Errors);
        Assert.Equal(0, app.Services.GetRequiredService<ReviewRuns>().Count);
        Assert.Empty(app.Services.GetRequiredService<StoredProcesses>().States);
    }

    [Fact]
    public async Task UnknownStep_IsRejectedBeforeCreatingAProcess()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var request = new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest { StepName = "NoSuchStep", Request = JsonSerializer.SerializeToElement(new { }, KaleidoJsonOptions.Options) }],
        };

        using var response = await client.PostAsJsonAsync("/catalog/1/processes/execute", request, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(ProcessorErrorCodes.UnknownStep, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        Assert.Empty(app.Services.GetRequiredService<StoredProcesses>().States);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string code, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/catalog/1/processes/execute")
        {
            Content = JsonContent.Create(new ExecuteProcessRequest
            {
                Steps = [new ProcessStepRequest
                {
                    StepName = nameof(ReviewOrderStep),
                    Request = JsonSerializer.SerializeToElement(new ReviewOrderStep { Code = code }, KaleidoJsonOptions.Options),
                }],
            }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpClient Client(WebApplication app, string[]? roles = null, string user = "alice")
    {
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(user, roles ?? ["reviewer"]));
        return client;
    }

    private static async Task<WebApplication> StartAsync(bool handOff = false, KaleidoAuthorizationMode authorizationMode = KaleidoAuthorizationMode.Authenticated,
        bool includePolicyStep = false, bool superuserHandler = false, bool includeAnonymousStep = false, bool includeOpenStep = false,
        bool includeVisibility = false, bool includeFrameworkMessages = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        if (includePolicyStep)
        {
            builder.Services.AddAuthorization(options => options.AddPolicy("clinician-only", policy => policy.RequireRole("clinician")));
        }

        if (superuserHandler)
        {
            builder.Services.AddSingleton<IAuthorizationHandler, SuperuserRoleHandler>();
        }
        builder.Services.AddSingleton<ReviewRuns>();
        builder.Services.AddSingleton<AnonymousRuns>();
        builder.Services.AddSingleton(new StoredProcesses());
        builder.Services.AddSingleton(new StoredIdempotency());
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(ProcessHttpServerFunctionalTests).Assembly],
            TypeFilter = type => includeVisibility
                ? type == typeof(VisibilityStartStep) || type == typeof(VisibilityStartHandler)
                    || type == typeof(VisibilityClerkStep) || type == typeof(VisibilityClerkHandler)
                    || type == typeof(VisibilityEscalateStep) || type == typeof(VisibilityEscalateHandler)
                    || type == typeof(VisibilityAdminStep) || type == typeof(VisibilityAdminHandler)
                : handOff ? type == typeof(HandOffStep) || type == typeof(HandOffHandler)
                    || type == typeof(SelfHandOffStep) || type == typeof(SelfHandOffHandler)
                : type == typeof(ReviewOrderStep) || type == typeof(ReviewOrderHandler)
                    || type == typeof(AdminOnlyStep) || type == typeof(AdminOnlyHandler)
                    || (includePolicyStep && (type == typeof(ClinicianStep) || type == typeof(ClinicianHandler)))
                    || (includeAnonymousStep && (type == typeof(PublicReviewStep) || type == typeof(PublicReviewHandler)))
                    || (includeOpenStep && (type == typeof(OpenFollowUpStep) || type == typeof(OpenFollowUpHandler))),
        })
            .AddEventing()
            .AddProcess()
            .AddContextStore<TestProcessContextStore>()
            .AddIdempotencyStore<TestIdempotencyStore>()
            .AddServer(new() { AuthorizationMode = authorizationMode })
            .AddHttp()
            .AddProcessHttpServer(new KaleidoProcessHttpOptions { IncludeFrameworkMessages = includeFrameworkMessages });
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapProcessor();
        app.MapGet("/ordinary", () => Results.Text("host"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    public sealed class ReviewRuns
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Increment() => Interlocked.Increment(ref _count);
    }

    [ProcessStep(DisplayName = "Review order", Description = "Reviews an order")]
    [KaleidoAuthorization(Roles = "reviewer")]
    public sealed record ReviewOrderStep : IProcessStep
    {
        public required string Code { get; init; }
    }

    public sealed class ReviewOrderHandler(ReviewRuns runs) : IProcessStepHandler<ReviewOrderStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(ReviewOrderStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            runs.Increment();
            return Task.FromResult(ProcessStepHandlerResult.Success());
        }
    }

    [ProcessStep(DisplayName = "Clinician review", Description = "Reviews a process under the host policy")]
    [KaleidoAuthorization(Policy = "clinician-only")]
    public sealed record ClinicianStep : IProcessStep;

    public sealed class ClinicianHandler : IProcessStepHandler<ClinicianStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(ClinicianStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    private sealed class SuperuserRoleHandler : AuthorizationHandler<RolesAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RolesAuthorizationRequirement requirement)
        {
            if (context.User.IsInRole("superuser"))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public sealed class AnonymousRuns
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        public void Increment() => Interlocked.Increment(ref count);
    }

    [ProcessStep(DisplayName = "Public review", Description = "A step anyone may submit")]
    [KaleidoAuthorization(AllowAnonymous = true)]
    public sealed record PublicReviewStep : IProcessStep;

    public sealed class PublicReviewHandler(AnonymousRuns runs) : IProcessStepHandler<PublicReviewStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(PublicReviewStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            runs.Increment();
            return Task.FromResult(ProcessStepHandlerResult.Success());
        }
    }

    [ProcessStep(DisplayName = "Open follow up", Description = "Any authenticated caller may continue")]
    [AvailableAfter<ReviewOrderStep>]
    public sealed record OpenFollowUpStep : IProcessStep;

    public sealed class OpenFollowUpHandler : IProcessStepHandler<OpenFollowUpStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(OpenFollowUpStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Visibility start", Description = "Opens a caller-visible process")]
    [KaleidoAuthorization(Roles = "vis-clerk,vis-admin")]
    public sealed record VisibilityStartStep : IProcessStep;

    public sealed class VisibilityStartHandler : IProcessStepHandler<VisibilityStartStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(VisibilityStartStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Clerk work", Description = "A clerk records the process")]
    [KaleidoAuthorization(Roles = "vis-clerk,vis-admin")]
    [DependsOn<VisibilityStartStep>]
    [AvailableUntil<VisibilityAdminStep>]
    public sealed record VisibilityClerkStep : IProcessStep;

    public sealed class VisibilityClerkHandler : IProcessStepHandler<VisibilityClerkStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(VisibilityClerkStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Escalation", Description = "Requires admin work to continue")]
    [KaleidoAuthorization(Roles = "vis-clerk")]
    [DependsOn<VisibilityStartStep>]
    public sealed record VisibilityEscalateStep : IProcessStep;

    public sealed class VisibilityEscalateHandler : IProcessStepHandler<VisibilityEscalateStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(VisibilityEscalateStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success<VisibilityAdminStep>());
    }

    [ProcessStep(DisplayName = "Admin work", Description = "Resolves a clerk escalation")]
    [KaleidoAuthorization(Roles = "vis-admin")]
    [DependsOn<VisibilityStartStep>]
    public sealed record VisibilityAdminStep : IProcessStep;

    public sealed class VisibilityAdminHandler : IProcessStepHandler<VisibilityAdminStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(VisibilityAdminStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Admin review", Description = "Reviews the order after its first step")]
    [AvailableAfter<ReviewOrderStep>]
    [KaleidoAuthorization(Roles = "admin")]
    public sealed record AdminOnlyStep : IProcessStep;

    public sealed class AdminOnlyHandler : IProcessStepHandler<AdminOnlyStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(AdminOnlyStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Hand off", Description = "Hands a process to a versioned target")]
    [KaleidoAuthorization(Roles = "reviewer")]
    public sealed record HandOffStep : IProcessStep;

    public sealed class HandOffHandler : IProcessStepHandler<HandOffStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(HandOffStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.HandOff("target", Target(context), "BeginStep"));

        internal static ProcessResult Target(ProcessStepContext context) => new()
        {
            ProcessId = context.ProcessId,
            ServiceVersion = "2",
            State = ProcessExecutionState.AwaitingStepSelection,
            AvailableSteps = ["FinishStep"],
            Steps = [new ProcessStepResult
            {
                StepName = "BeginStep",
                CandidateStatus = StepCandidateStatus.Built,
                IncludedInExecutionPlan = true,
                ExecutionStatus = StepExecutionStatus.Completed,
            }],
        };
    }

    [ProcessStep(DisplayName = "Self hand off", Description = "Attempts to hand a process to its own service")]
    [KaleidoAuthorization(Roles = "reviewer")]
    public sealed record SelfHandOffStep : IProcessStep;

    public sealed class SelfHandOffHandler : IProcessStepHandler<SelfHandOffStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(SelfHandOffStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.HandOff("catalog", HandOffHandler.Target(context), "BeginStep"));
    }

    public sealed class StoredProcesses
    {
        private int _refuseNextSave;
        public Dictionary<ProcessKey, ProcessContext> States { get; } = [];
        public object Gate { get; } = new();
        public void RefuseNextSave() => Volatile.Write(ref _refuseNextSave, 1);
        public bool TakeRefusal() => Interlocked.Exchange(ref _refuseNextSave, 0) == 1;
    }

    public sealed class TestProcessContextStore(StoredProcesses state) : IProcessContextStore
    {
        public Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                return Task.FromResult(state.States.GetValueOrDefault(key));
            }
        }

        public Task<ProcessContext> SaveAsync(ProcessContext context, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                if (state.TakeRefusal())
                {
                    throw new ProcessConcurrencyException(context.ProcessId);
                }

                var stored = state.States.GetValueOrDefault(context.Key);
                if ((stored?.Version ?? 0) != context.Version)
                {
                    throw new ProcessConcurrencyException(context.ProcessId);
                }

                var saved = context with { Version = context.Version + 1 };
                state.States[context.Key] = saved;
                return Task.FromResult(saved);
            }
        }
    }

    public sealed class StoredIdempotency
    {
        public Dictionary<string, IdempotencyRecord> Records { get; } = new(StringComparer.Ordinal);
        public object Gate { get; } = new();
    }

    public sealed class TestIdempotencyStore(StoredIdempotency state, TimeProvider time) : IProcessIdempotencyStore
    {
        public Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                if (state.Records.TryGetValue(admission.Key, out var existing) && existing.ExpiresUtc > time.GetUtcNow())
                {
                    return Task.FromResult<IdempotencyRecord?>(existing);
                }

                state.Records[admission.Key] = admission;
                return Task.FromResult<IdempotencyRecord?>(null);
            }
        }

        public Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                if (!state.Records.TryGetValue(result.Key, out var existing)
                    || existing.AdmissionId != result.AdmissionId
                    || existing.State != IdempotencyState.InProgress
                    || existing.ExpiresUtc <= time.GetUtcNow())
                {
                    return Task.FromResult(false);
                }

                state.Records[result.Key] = result;
                return Task.FromResult(true);
            }
        }

        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken)
        {
            lock (state.Gate)
            {
                return Task.FromResult(state.Records.TryGetValue(key, out var existing)
                    && existing.AdmissionId == admissionId
                    && existing.State == IdempotencyState.InProgress
                    && existing.ExpiresUtc > time.GetUtcNow()
                    && state.Records.Remove(key));
            }
        }
    }
}
