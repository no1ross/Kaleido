# Kaleido.Http.Mcp

Publishes a Kaleido host's capabilities to AI agents as [MCP](https://modelcontextprotocol.io)
tools. The tools are generated from the registry (the host's own capabilities, or the aggregate
of its clients on a router), filtered per caller, and every call runs **as the caller**, through
the Kaleido HTTP clients with the caller's token forwarded.

```csharp
builder.Services
    .AddKaleido(builder.Configuration, o => o.ServiceName = "router")
    .AddHttp()
    .AddHttpClients()
    .AddMcp(o => o.ServerName = "priorauth");

app.MapKaleidoHttp(o => o.AggregateRegistry = true);
app.MapKaleidoMcp("/mcp");
```

Configuration (`Kaleido:Mcp`): `ServerName`, `ServerVersion`, `Instructions` or
`InstructionsFile` (replace the default instructions), `AdditionalInstructions` (appended: the
business's guidance for its agents), `MaxElicitationRounds` (zero or more, default 5; 0 returns
every information request to the agent; a negative value fails startup with `mcp_invalid_options`),
`ToolListing` (`All`, the default, or `FrontDoor`; see [Front door](#front-door)) and `ListedTools`
(FrontDoor only: extra tool names to list).

Call `AddHttp()` and `AddHttpClients()` (with at least one client under `Kaleido:Clients`)
**before** `AddMcp()`: it fails startup with `mcp_missing_http` or `mcp_missing_clients`
otherwise. An `InstructionsFile` that does not exist fails with
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
64 characters is shortened with a hash suffix and logged at Warning; KAL2020 and KAL2019 keep the
service name (15) and the capability part (40) inside the budget. When two capabilities produce the
same name, the later one says what it is (`_view`, `_source`, `_step`), and the collision is logged.

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
| `idempotentHint` | `true` | `true` unless `[Repeatable]` (a non-repeatable step runs once per process) |
| description | | `RequiresConfirmation`: "Ask the user to confirm before calling." (a hint, not enforced) |

Classified fields (`DataClassificationAttribute`, e.g. `[PHI]`) are marked in input descriptions
("Sensitive (PHI).") and in the "Returns:" list (`Name (PHI)`), so an agent does not repeat or store
them carelessly.

## After a step

A step's result is summarized for the agent: the outcome, the step's result, `next`, and the two
message streams of the step response, kept apart:

- **`messages`**: the handler's business messages, for the user, delivered verbatim (ADR 0007).
- **`diagnostics`**: Kaleido's framework messages (`pro_*`) that are warnings or errors, for the agent
  to explain; informational notes are left out. They are present only when the service returns
  framework messages (`AddHttp(o => o.IncludeFrameworkMessages = true)`).

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
PriorAuth sample rejects an enrollment that is not the member's, and a provider location that is
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
| `mcp_internal_error` | An unexpected error in the adapter. | Same as above. |

**Retries are safe.** Tool calls go through the Kaleido clients: transient failures are retried by their resilience pipeline, and every step call carries an `Idempotency-Key`, so a retry the service has already answered gets the stored answer instead of running the step twice ([`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md#resilience)).

## Security

- Tools are built from the registry **filtered for the caller**: a caller sees only what it may use.
- **Tool changes follow deployments within the registry check interval:** the tool list is built from the host's registry snapshot, which an aggregating host rebuilds after `RegistryCacheTtl` (default 30 seconds), checking each downstream with its ETag. A removed step that is still called answers 404 and drops that downstream's snapshot at once.
- Every call forwards the caller's token (`ForwardCaller`), so the services authorize the user,
  not the adapter.
- Questions and lookups go to the user through forms where the client supports them; the model
  never answers for the user.

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
    .AddMcp(o => o.ResourceMetadata = new ProtectedResourceMetadata
    {
        AuthorizationServers = ["https://login.microsoftonline.com/{tenant}/v2.0"],
        ScopesSupported = ["api://kaleido-mcp/mcp.access"],
    });

app.MapKaleidoMcp("/mcp").RequireAuthorization();
```

- **The token is forwarded unchanged** to the Kaleido services (`ForwardCaller`); per-caller tool
  filtering and authorization work exactly as with a header token. The services must accept that
  token (same audience), or the host exchanges it (on-behalf-of) before forwarding - the host's
  concern.
- **Client registration** (a pre-registered client id, or dynamic client registration) is the
  identity provider's concern; check which one your MCP clients use.
- Without `RequireAuthorization()` anonymous callers stay allowed and see only anonymous tools; the
  challenge (and with it the sign-in) happens only for an endpoint that requires a caller.
- The sample keeps its dev tokens.