# Kaleido.Http.Mcp

Publishes a Kaleido host's capabilities to AI agents as [MCP](https://modelcontextprotocol.io)
tools. The tools are generated from the registry (the host's own capabilities, or the aggregate
of its clients on a router), filtered per caller, and every call runs **as the caller**, through
the Kaleido HTTP clients with the caller's token forwarded.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../docs/guide/README.md). This README is the reference for the project.

```csharp
builder.Services.AddAuthentication().AddJwtBearer();   // the host's own scheme: callers are authenticated by default
builder.Services.AddAuthorization();

builder.Services
    .AddKaleido(builder.Configuration, new()
    {
        ServiceName = "router",
        Assemblies = [typeof(Program).Assembly], // required, even when the router has no capabilities of its own
    })
    .AddHttp()
    .AddHttpClients()
    .ForwardCallerToken()          // how tool calls are authorized for the agent's user (see Security)
    .AddMcp(new() { ServerName = "priorauth" });

app.UseAuthentication();
app.UseAuthorization();
app.MapKaleidoHttp(new() { AggregateRegistry = true });
app.MapKaleidoMcp("/mcp").RequireAuthorization();
```

A complete, tested single-service host is the guide's
[Publish it to AI agents](../../docs/guide/getting-started.md#6-publish-it-to-ai-agents-mcp); for an
OAuth-protected endpoint, register a per-service exchange instead of `ForwardCallerToken()` (see
Security, [ADR 0027](../../docs/decisions/0027-forward-caller-token-kept-with-guards.md)).

Options (`KaleidoMcpOptions`, passed to `AddMcp(new() { ... })`; the `Kaleido:Mcp` configuration
section overrides them setting by setting, ADR 0030): `ServerName`, `ServerVersion`, `Instructions`
or `InstructionsFile` (replace the default instructions; set one, not both),
`AdditionalInstructions` (appended: the business's guidance for its agents), `MaxElicitationRounds`
(zero or more, default 5; 0 returns every information request to the agent), `ToolListing` (`All`,
the default, or `FrontDoor`; see [Front door](#front-door)), `ListedTools` (FrontDoor only: extra
tool names to list) and `RequireConfirmation` (default `false`; see [Confirmation](#confirmation)).
Nothing is trimmed or replaced: a blank name, version or instructions setting, a blank or repeated
listed tool, a negative round count or both instruction settings at once fail startup with
`mcp_invalid_options`.

Call `AddHttp()`, `AddHttpClients()` (with at least one client under `Kaleido:Clients`) and a
call-credential registration (`ForwardCallerToken()` or `AddCallCredentials<TProvider>()`)
**before** `AddMcp()`: it fails startup with `mcp_missing_http`, `mcp_missing_clients` or
`mcp_call_credentials_not_configured` otherwise. An `InstructionsFile` that does not exist fails with
`mcp_missing_instructions_file`. The endpoint is stateless Streamable HTTP, so it needs no session
affinity behind a load balancer.

## Tools

| Tool | From | What it does |
|---|---|---|
| `{service}_{source or view}` | each query source and view | Runs the query (search text, filters, sort, page, view parameters). Takes an optional `processId` where the host has processes (see below). |
| `{service}_{step}` | each step | Executes the step; a step that is not initial needs the `processId`. |
| `answer_information_request` | built in, when the host (or its aggregate) has processes | Submits the user's answers to the questions a process is waiting for. |
| `get_process_state` | built in, when the host (or its aggregate) has processes | Where a process stands: state, completed and failed steps, next steps. |
| `search_tools`, `describe_tool`, `call_tool` | built in, FrontDoor only | Find a tool by what it does; read one tool's full inputs; run any of the caller's tools by name. |

**Queries and the process id.** A query tool's `processId` argument is the adapter's, not the
source's: the query is sent inside `IKaleidoProcessForwarding.ForwardProcess(processId)`, so it
carries `X-Kaleido-Process-Id` and the service records it with the process (traces, logs,
`QueryExecuted`). The `/mcp` request itself has no process id header, so without the argument an
agent's queries are not tied to any process. A source that declares its own `processId` parameter
(a search scoped to the request) gets it filled from the same argument; the parameter is not
repeated under `parameters`, so there is one `processId`, always at the top level (a nested
`parameters.processId` is still accepted). It is required only when that parameter is. The result
echoes the `processId`.

**Required arguments are checked first.** A query whose required argument is missing (top level,
or a required member of `parameters`) is answered with `mcp_invalid_arguments` naming the field
and quoting its description; nothing is sent to the service.

Names are `{service}_{capability}` in snake case, where the capability part is the type name
without a `QueryContextSource`, `QueryViewSource`, `ViewSource`, `Source` or `Step` suffix
(`MemberSearchSource` in service `member` becomes `member_member_search`). A name longer than MCP's
64 characters is shortened with a hash suffix and logged at Warning; KAL2017 and KAL2016 keep the
service name (15) and the capability part (40) inside the budget.

**Names are stable** (#435). They are derived from every capability the host publishes (the
unfiltered registry), not from what one caller may see, and not from the order of the list. When two
capabilities produce the same name (for example a source and its view, or two views of different
sources), or a capability produces a built-in tool's name, none of them gets the plain name: each gets
`_` plus eight characters of a hash of its own identity (service, kind, source, type name), and the
collision is logged at Warning so the owner can rename. A name therefore always means the same
capability; removing one side of a collision in a later deployment gives the other its plain name
back. A hashed name shows that some capability shares the stem, even one the caller cannot see; it
names nothing the caller can call.

Step and input descriptions are the published metadata (`[ProcessStep]`, `[Description]`,
`[Display]`): they are the interface the agent reads (ADR 0007). A query field's description is
shown where the field first appears in the tool (`filters`, else `searchText`, else `sort`). When
the tools are built (the first request, and again whenever the registry changes) the adapter logs, at Warning, every capability, input and query field without a
description, once per property (`service.Source.Field`), not once per tool.

**Tool hints** ([ADR 0015](../../docs/decisions/0015-declared-step-behavior.md)) - what the implementer declared on
`[ProcessStep]`, so a client knows which calls need extra care:

| MCP hint / description | Query tools | Step tools |
|---|---|---|
| `readOnlyHint` | `true` | `false` |
| `destructiveHint` | `false` | `Destructive` (default `false`); description: "Cannot be undone." |
| `openWorldHint` | `false` | `External` (default `false`); description: "Reaches outside systems." |
| `idempotentHint` | `true` | `true` only for a non-`[Repeatable]` step that continues a process (once completed, it is not run again); `false` for an initial step (called again without a `processId`, it starts another process) and a `[Repeatable]` one (#440) |
| description | | `RequiresConfirmation`: "Ask the user to confirm before calling." (a hint); with `RequireConfirmation` on, "The user is asked to confirm in a form before it runs" (see [Confirmation](#confirmation)) |

### Confirmation

`[ProcessStep(RequiresConfirmation = true)]` is a hint by default. With
`Kaleido:Mcp:RequireConfirmation` (`KaleidoMcpOptions.RequireConfirmation`, off by default) the
adapter asks the user itself before running such a step, whichever way it is reached (its own tool,
`call_tool`, or a guided lookup), and runs it only when the user accepts (#439):

| Client | What happens |
|---|---|
| shows forms in the session | a form names the step, what it declares ("It cannot be undone.") and its inputs, with one unchecked "Run ... now" checkbox; the step runs only on accept with the box checked |
| MRTR (2026-07-28) | the same form as an `input_required` round (`kaleido-confirm`); the retry runs the confirmed call only with an accepted, checked answer |
| no forms | `mcp_confirmation_required`; nothing runs, and the user runs the step from a client that shows forms |

Declining, cancelling, leaving the box unchecked, or retrying without an answer returns
`outcome: "notRun"` and runs nothing. What the agent says, or a `confirm` argument it adds, is never a
confirmation. This raises the bar, not proof of consent: the client relays the form and its answer,
and Kaleido cannot tell a person from a client that answers forms itself. A step whose consent must be
proven checks it in its service (for example a signed approval in its input).

Classified fields (`DataClassificationAttribute`, e.g. `[PHI]`) are marked in input descriptions
("Sensitive (PHI).") and in the "Returns:" list (`Name (PHI)`), so an agent does not repeat or store
them carelessly.

**What the returned fields mean.** A query result or a step result comes with `fieldDescriptions`:
the registry's description of each field the result contains (from `[Description]` on the query
context, the view's output record or the step's result type; KAL2015 and KAL2021 ask for them). Tool
descriptions keep listing only the field names, so the tool list stays short and the meaning arrives
with the data (#213).

## After a step

A step's result is summarized for the agent: the outcome, the step's result, `next`, and the two
message streams of the step response, kept apart:

- **`messages`**: the handler's business messages, for the user, delivered verbatim (ADR 0007).
- **`diagnostics`**: Kaleido's framework messages (`pro_*`) that are warnings or errors, for the agent
  to explain; informational notes are left out. They are present only when the service returns
  framework messages (`AddHttp(new() { IncludeFrameworkMessages = true })`).

The JSON summary is one text block marked for the assistant (MCP `annotations.audience`). Each
business message is also sent as its own text block marked for the user (`audience: ["user"]`,
`priority: 1`), and so is each reason a service gives for refusing a call in its own words (an error
code that is not Kaleido's: not `qry_*`, `pro_*`, `httpclient_*`, `mcp_*`, `argument_error` or
`framework_error`). A client that honours the annotation can show that text to the user as written,
without the model in between (ADR 0007). A client that ignores it passes every block to the model,
and the instructions tell the model to relay messages verbatim.

`next`:

- **`next.required`**: the step that must run next. For an information step, `next.informationRequest`
  carries the questions, with an instruction to ask them exactly as written, to read proposed
  answers back for confirmation, and never to answer for the user.
- **`next.available`**: the steps the user may choose from, each with what it **closes** (its
  `availableUntil`), so the user decides the order knowing what each choice ends. The agent offers
  them; it does not pick.
- **After a handoff** the target service's state is read, so `next` names that service's tools.
- **A rejected step** (`outcome: rejected`) changed nothing: the note says so and `next` lists the
  unchanged next steps (ADR 0008). A step that failed unexpectedly while the process still has next
  steps is reported the same way.

### Questions as forms

When the client can show forms, questions go to the **user**, not the model:

- clients on the `2026-07-28` revision (MRTR) get "input required" with the form, and call the tool
  again with the answers; nothing is held between calls;
- otherwise the questions are returned in `next.informationRequest` for the agent to ask.

The endpoint is stateless (no session affinity needed), so clients on `2025-11-25` or earlier, which
rely on a session for server-to-client requests, are asked through the agent even when they can show
forms. Sessions for those clients are an opt-in not offered yet
([#350](https://github.com/no1ross/Kaleido/issues/350)).

Proposed answers open the form pre-filled; the user confirms or corrects them.

### Conditional questions

A question with `enableWhen` applies only when earlier answers say so
([ADR 0024](../../docs/decisions/0024-conditional-questions.md)). The adapter applies the conditions;
the agent never sees them and only asks what it is handed:

- **Forms** (MRTR and session) show one round at a time: first the questions with no condition, then
  those the answers so far make apply. The answers given so far travel in the continuation state, and
  one complete submission reaches the service. A form declined, or accepted without answers, submits
  nothing.
- **Chat:** `next.informationRequest` lists only the questions that apply now, without condition
  fields. When the answers given to `answer_information_request` make further questions apply, the
  result returns them with an `answersToken` and submits nothing. The agent asks those, then calls
  `answer_information_request` again with the new answers and the token. When nothing more applies,
  everything is submitted and the service validates it as always. An answer to a question that does
  not apply is not sent.
- The token holds the earlier answers for that process and request only; a token for anything else is
  ignored. It is not a security boundary: the service validates the complete submission.

## Lookups

A step input with `[Lookup]` takes its value from one result of a query (e.g. `MemberId` and
`MemberEnrollmentId` from the same Member Search row). The tool's description tells the agent to
search first, show the matches by the lookup's display fields, let the user pick one, and take the
grouped inputs from the same result.

**Guided lookups.** When the client can show forms and the agent calls the step **without** a
lookup's inputs, the adapter runs the lookup itself:

1. the user gets a search form ("Find Member Search");
2. the query runs as the caller, sent with the step's `processId` (and filled into the query's own
   `processId` parameter when it declares one);
3. the user picks one result from a single-choice form labelled with the display fields (or
   searches again);
4. the adapter fills the grouped inputs from the chosen row and runs the step.

The ids never pass through the model. Clients without forms keep the instruction path.

**Lookups guide; services check.** A lookup reduces mistakes; it does not enforce anything. The
continuation state of a guided lookup is opaque but not trusted, and an agent can still pass ids
itself. The service that owns the step checks what it receives and rejects a mismatch (the
[PriorAuth sample](https://github.com/no1ross/Kaleido-Samples-PriorAuth) rejects an enrollment that is not the member's, and a provider location that is
not the provider's or not offered for the request, and reads names and network status from the
chosen result instead of taking them from the caller).

## Front door

With `Kaleido:Mcp:ToolListing = FrontDoor`
([ADR 0016](../../docs/decisions/0016-mcp-front-door-tool-listing.md)), `tools/list` holds only:

- the steps that can start a process;
- the queries those steps declare with `[Lookup]` (what an agent needs before a process exists);
- the names in `Kaleido:Mcp:ListedTools` (searches no starting step declares, e.g. "previous requests");
- the built-ins, including `search_tools`, `describe_tool` and `call_tool`.

Every other tool is still built and callable, with the same authorization and process rules, and
the list does not grow with the number of non-initial steps or unlisted queries. Names in
`ListedTools` that are not tools of the caller are logged at Warning.

- **`next` describes each open step that is not listed**: its `description`, `inputs` (the tool's
  input schema), `hints` (`readOnly`, `destructive`, `idempotent`), and per lookup what it `fills`
  and the `search` to run (tool, inputs, the processId already in `arguments`, the fields to
  `show`). `howToRun` says to run them with `call_tool`.
- **`call_tool`** (`name`, `arguments`) runs the named tool exactly as if it had been called: the
  handler unwraps it before anything else, MRTR retries included. It is annotated not read-only and
  destructive, so clients keep asking the user before each call; the instructions also tell the
  agent to confirm before running a step.
- **`search_tools`** (`text`, optional `kind`) ranks the caller's tools by the words they mention;
  **`describe_tool`** (`name`) returns one tool in full. Neither calls a service.
- The packaged front-door instructions are appended to the instructions (before
  `AdditionalInstructions`).

Use `All` for small hosts: every tool keeps its own typed definition and its own approval prompt.

## When a call fails

A tool call never fails at the protocol level: a problem comes back as a tool result with `isError: true` and one of the `McpErrorCodes`, so the agent can explain it or recover.

| Code | When | What the agent is told to do |
|---|---|---|
| `mcp_unknown_tool` | The tool (or `call_tool`'s / `describe_tool`'s `name`) is not offered to this caller. | Use the current tool list, `next` or `search_tools`. |
| `mcp_invalid_arguments` | A required argument is missing (named, with its description) or has the wrong shape. | Fix the arguments. |
| `mcp_service_error` | The service refused or failed the call; its status and errors are included. A 5xx without errors does not pass the service's message through. | Explain the errors to the user. For a 5xx: check the arguments against the tool's inputs; if they are right, tell the user and do not repeat the same call. |
| `mcp_process_conflict` | Another call changed the process first (409); nothing was saved. | Read the process with `get_process_state` and decide with the user - never repeat the call blindly. |
| `mcp_service_unavailable` | The service could not be reached, or no client is configured for it. | For a step, check the process with `get_process_state` before trying again. |
| `mcp_no_pending_information_request` | `answer_information_request` for a process that is not waiting for answers (already answered, or it needs a step instead). | Read the process with `get_process_state` and do what it needs now. |
| `mcp_confirmation_required` | A step that needs the user's confirmation (`RequireConfirmation`), in a client that cannot show forms; nothing was run. | Tell the user; they run the step from a client that shows forms. Never claim the user agreed instead. |
| `mcp_internal_error` | An unexpected error in the adapter. | Same as above. |

**Retries.** Tool calls go through the Kaleido clients: their resilience pipeline retries transient failures of one call with the same `Idempotency-Key`, so the service answers a retry it has already completed with the stored answer instead of running the step twice ([`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md#resilience)). When the *agent* calls a tool again (after a timeout, a lost answer, a restart), that is a new call; the adapter gives it the same key only when it is the same logical call (#440):
- a non-`[Repeatable]` step continuing a process, with the same inputs for the same process: same key, so the stored answer comes back (within the service's idempotency retention);
- answers to one information request: same key for the same answers;
- a `[Repeatable]` step, or an initial step starting a new process (no `processId`): a new key each time, because two such calls are two operations. Here a repeated call runs again; read the process (`get_process_state`) before repeating one whose outcome is unknown.

Keys are scoped per service and caller, so two users never share an answer.

## Trust boundary

The agent (the model) and the MCP client are **untrusted input**. Everything that protects a business
runs on the server, whatever the agent says or the client shows:

| The agent can... | What decides | Where |
|---|---|---|
| call any tool name | the caller's per-service access (the tool list is filtered per caller; a tool not in it is `mcp_unknown_tool` and the service is never called) | adapter + each service's authorization |
| pass any arguments | each service's validation (`DataAnnotations`, query rules) | service |
| claim the user agreed | nothing by default: prompt guidance (`confirm with the user`, destructive hints) is advice to well-behaved clients, **not consent**. With `RequireConfirmation`, a `RequiresConfirmation` step runs only after the user accepts the adapter's own form ([Confirmation](#confirmation)): the agent's claim is never accepted, but the client relays the form, so it is still not proof of consent. Questions that must come from the user are information requests: shown as forms where the client supports them, refused when the answers do not match the pending request | service + adapter |
| put instructions in service text | nothing technical: tool and field descriptions, business messages and refusal reasons are written by the services the adapter aggregates and go to the model as they are. A compromised or careless service can try to steer the agent through them. The default instructions tell the agent to treat that text as data, never as instructions; the controls above (per-caller tools, service validation, the confirmation round) do not depend on the agent obeying | each service's authors (review what a service publishes) |
| retry or abandon a call | idempotency keys on execute calls; a cancellation is recorded as one (a Warning), never a failure | client + service |
| learn data it should not | which capabilities the caller may use (tools are filtered per caller; each service authorizes its own); which **records** a caller may see within a capability is the service's own logic (no case-level guardrails). Credentials and tool arguments are never written to logs or span tags | adapter + services |

The credential a tool call carries is the host's decision and never the host's own identity
([ADR 0022](../../docs/decisions/0022-call-credentials.md)); downstream services see that the call came
through MCP and which tool started it (`Origin`/`OriginTool`, informational) and which application
acted (`CallerActor`, from the token) - see [`docs/CORRELATION.md`](../../docs/CORRELATION.md).
Each row is covered by tests: `McpEndpointTests`, `McpContractTests` and `McpTrustTests` in
`tests/Kaleido.Http.Mcp.FunctionalTests`.

## The instructions resource

The adapter sends **server instructions** to every MCP client: how to use Kaleido's tools (start
from `next`, pass the `processId` on every call, relay business messages verbatim, ask the user
rather than invent answers). They are packaged with the adapter (`Instructions/DefaultInstructions.md`,
an embedded resource, plus `FrontDoorInstructions.md` for the front door) and versioned **with the
package**: they change only in a Kaleido release, and a change is listed in the release notes, because
agents' behavior depends on them.

- `Kaleido:Mcp:AdditionalInstructions` **appends** the business's own guidance (the usual choice:
  Kaleido's mechanics stay current with upgrades).
- `Instructions` / `InstructionsFile` **replace** them entirely; a host doing so owns keeping them
  in step with the adapter's tools after each upgrade.
- `ServerVersion` (default `1.0.0`) is the host's own version of what it publishes, reported to
  clients; raise it when the tools or instructions a client sees change meaningfully.

The instructions are guidance for well-behaved agents; nothing in them is a security control (see
[Trust boundary](#trust-boundary)).

## Integration example

A router publishing two services to AI agents, with OAuth sign-in from MCP clients and per-service
tokens:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o => { o.Authority = "https://login.example.com"; o.Audience = "api://kaleido-mcp"; })
    .AddMcp(new() { ResourceMetadata = new ProtectedResourceMetadata
    {
        AuthorizationServers = ["https://login.example.com"],
        ScopesSupported = ["api://kaleido-mcp/mcp.access"],
    } });
builder.Services.AddAuthorization();

builder.Services
    .AddKaleido(builder.Configuration, new()
    {
        ServiceName = "router",
        Assemblies = [typeof(Program).Assembly],
        AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
    })
    .AddHttp()
    .AddHttpClients()                                   // Kaleido:Clients:intake, Kaleido:Clients:radiology
    .AddCallCredentials<OnBehalfOfProvider>()           // a token for the user, per service (Kaleido.Http.Client README)
    .AddMcp(new() { ServerName = "priorauth" });

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapKaleidoHttp(new() { AggregateRegistry = true });
app.MapKaleidoMcp("/mcp").RequireAuthorization();
app.Run();
```

```json
{
  "Kaleido": {
    "Clients": {
      "intake": { "BaseUrl": "https://intake.internal/" },
      "radiology": { "BaseUrl": "https://radiology.internal/" }
    },
    "Mcp": { "AdditionalInstructions": "Start every request with an intake." }
  }
}
```

An agent connecting to `/mcp` signs the user in, lists the tools that user may use across both
services, and every call reaches the service with a token for that user and service. The
[PriorAuth sample](https://github.com/no1ross/Kaleido-Samples-PriorAuth)'s router is a complete, runnable version (with dev tokens and `ForwardCallerToken()`).

## Security

- Tools are built from the registry **filtered for the caller**: a caller sees only what it may use.
- **Tool changes follow deployments within the registry check interval:** the tool list is built from the host's registry snapshot, which an aggregating host rebuilds after `RegistryCacheTtl` (default 30 seconds), checking each downstream with its ETag. A removed step that is still called answers 404 and drops that downstream's snapshot at once.
- Every tool call is made **for the agent's user**: the host's `IKaleidoCallCredentialProvider`
  decides its credential (the user's token unchanged with `ForwardCallerToken()`, or a token
  exchanged per service), and Kaleido guarantees it is never the host's own service credential; an
  anonymous user stays anonymous ([ADR 0022](../../docs/decisions/0022-call-credentials.md),
  [`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md#who-a-call-is-authorized-as-ikaleidocallcredentialprovider)).
  The services authorize the user, not the adapter.
- Questions and lookups go to the user through forms where the client supports them; the model
  never answers for the user.
- A process id is a bearer handle: the agent passes it as a tool argument, and any caller who knows
  it and passes the step's own checks can read and continue that process (no case-level guardrails).
  What that exposes, and where a host enforces case access, is in the
  [threat model](../../docs/AUTHORIZATION.md#threat-model-the-process-id-is-a-bearer-handle).

## Sign-in from MCP clients (OAuth)

The host authenticates, as for every Kaleido endpoint; the adapter only uses the caller the host
established and forwards its token. What MCP adds is **discovery**: a client such as VS Code,
Claude or Devin that connects without a token gets `401` with
`WWW-Authenticate: Bearer resource_metadata="..."`, reads that protected-resource metadata
document (RFC 9728) to learn which identity provider issues tokens for the endpoint, signs the user
in there, and connects again with the token. Users do not paste tokens into the client.

Kaleido needs nothing for this: the MCP SDK's authentication scheme serves the metadata and answers
the challenge, next to the host's own token validation.

```csharp
builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;                 // validates tokens, as today
        o.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme; // 401 + resource_metadata
    })
    .AddJwtBearer(o =>
    {
        o.Authority = "https://login.microsoftonline.com/{tenant}/v2.0";          // any OIDC provider
        o.Audience = "api://kaleido-mcp";
    })
    .AddMcp(new() { ResourceMetadata = new ProtectedResourceMetadata
    {
        AuthorizationServers = ["https://login.microsoftonline.com/{tenant}/v2.0"],
        ScopesSupported = ["api://kaleido-mcp/mcp.access"],
    } });

app.MapKaleidoMcp("/mcp").RequireAuthorization();
```

- **What reaches the services is the host's choice** (ADR 0022): with `ForwardCallerToken()` the
  user's token unchanged - the services must accept a token issued for the MCP endpoint (one
  audience); with `AddCallCredentials<TProvider>()` a token the provider exchanges for each service
  (on-behalf-of), which the services validate against their own audience. Per-caller tool filtering
  and authorization work the same either way.
- **For an OAuth-protected MCP endpoint, exchange the token.** The MCP authorization specification
  forbids passing the access token issued for the MCP server through to downstream APIs ("token
  passthrough"): such a host registers `AddCallCredentials<TProvider>()`. `ForwardCallerToken()` is
  for deployments that deliberately share one audience, such as the samples' dev tokens
  ([ADR 0027](../../docs/decisions/0027-forward-caller-token-kept-with-guards.md)). Configure the
  clients with `https` base URLs; a non-HTTPS, non-loopback client is reported at startup.
- **Client registration** (a pre-registered client id, or dynamic client registration) is the
  identity provider's concern; check which one your MCP clients use.
- Without `RequireAuthorization()` anonymous callers stay allowed and see only anonymous tools; the
  challenge (and with it the sign-in) happens only for an endpoint that requires a caller.
- The sample keeps its dev tokens.
