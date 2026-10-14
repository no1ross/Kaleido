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
business's guidance for its agents), `MaxElicitationRounds`.

## Tools

| Tool | From | What it does |
|---|---|---|
| `{service}_{source or view}` | each query source and view | Runs the query (search text, filters, sort, page, view parameters). |
| `{service}_{step}` | each step | Executes the step; a step that is not initial needs the `processId`. |
| `answer_information_request` | built in | Submits the user's answers to the questions a process is waiting for. |
| `get_process_state` | built in | Where a process stands: state, completed and failed steps, next steps. |

Names come from the published names in snake case. When two capabilities produce the same name,
the later one says what it is (`_view`, `_source`, `_step`), and the collision is logged.

Step and input descriptions are the published metadata (`[ProcessStep]`, `[Description]`,
`[Display]`): they are the interface the agent reads (ADR 0007).

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

A step's result is summarized for the agent: the outcome, the business messages (verbatim),
the step's result, and `next`:

- **`next.required`**: the step that must run next. For an information step, `next.informationRequest`
  carries the questions, with an instruction to ask them exactly as written, to read proposed
  answers back for confirmation, and never to answer for the user.
- **`next.available`**: the steps the user may choose from, each with what it **closes** (its
  `availableUntil`), so the user decides the order knowing what each choice ends. The agent offers
  them; it does not pick.
- **After a handoff** the target service's state is read, so `next` names that service's tools.
- **A rejected step** (`outcome: rejected`) changed nothing: the note says so and `next` lists the
  unchanged next steps (ADR 0008). A step that failed unexpectedly while the process still has next
  steps is reported the same way. Framework messages are shown only when they are warnings or errors.

### Questions as forms

When the client can show forms, questions go to the **user**, not the model:

- clients on the stateless revisions (MRTR) get "input required" with the form, and call the tool
  again with the answers; nothing is held between calls;
- clients with a session get an elicitation request during the call;
- otherwise the questions are returned in `next.informationRequest` for the agent to ask.

Proposed answers open the form pre-filled; the user confirms or corrects them.

## Lookups

A step input with `[Lookup]` takes its value from one result of a query (e.g. `MemberId` and
`MemberEnrollmentId` from the same Member Search row). The tool's description tells the agent to
search first, show the matches by the lookup's display fields, let the user pick one, and take the
grouped inputs from the same result.

**Guided lookups.** When the client can show forms and the agent calls the step **without** a
lookup's inputs, the adapter runs the lookup itself:

1. the user gets a search form ("Find Member Search");
2. the query runs as the caller (with the step's `processId` when the query takes one);
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

## When a call fails

A tool call never fails at the protocol level: a problem comes back as a tool result with `isError: true` and one of the `McpErrorCodes`, so the agent can explain it or recover.

| Code | When | What the agent is told to do |
|---|---|---|
| `mcp_unknown_tool` | The tool is not (or no longer) offered to this caller. | Use the current tool list. |
| `mcp_invalid_arguments` | A required argument is missing or has the wrong shape. | Fix the arguments. |
| `mcp_service_error` | The service refused or failed the call; its status and errors are included. | Explain the errors to the user. |
| `mcp_process_conflict` | Another call changed the process first (409); nothing was saved. | Read the process with `get_process_state` and decide with the user - never repeat the call blindly. |
| `mcp_service_unavailable` | The service could not be reached, or no client is configured for it. | For a step, check the process with `get_process_state` before trying again. |
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