<p align="center">
  <img src="docs/assets/kaleido-readme-banner.png" alt="Kaleido - Typed. Discoverable. Stateful." width="100%" />
</p>

<p align="center">
  <strong>Build business services that describe their capabilities, execute their actions, and tell applications and AI how to continue.</strong>
</p>

<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-framework-512BD4?style=flat-square" />
  <img alt="Status: preview" src="https://img.shields.io/badge/status-preview-8B5CF6?style=flat-square" />
  <a href="LICENSE"><img alt="License: Apache 2.0" src="https://img.shields.io/badge/license-Apache--2.0-19A974?style=flat-square" /></a>
  <img alt="Machine discoverable" src="https://img.shields.io/badge/capabilities-machine--discoverable-3182F6?style=flat-square" />
  <a href="src/Mcp/Mcp/README.md"><img alt="AI interface: MCP" src="https://img.shields.io/badge/AI%20interface-MCP-3182F6?style=flat-square" /></a>
</p>

<p align="center">
  <a href="https://github.com/no1ross/Kaleido/actions/workflows/build.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/no1ross/Kaleido/build.yml?branch=master&amp;style=flat-square&amp;label=build" /></a>
  <a href="https://github.com/no1ross/Kaleido/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://img.shields.io/github/actions/workflow/status/no1ross/Kaleido/codeql.yml?branch=master&amp;style=flat-square&amp;label=CodeQL" /></a>
  <a href="https://sonarcloud.io/summary/new_code?id=no1ross_Kaleido"><img alt="Quality gate" src="https://img.shields.io/sonar/quality_gate/no1ross_Kaleido?server=https%3A%2F%2Fsonarcloud.io&amp;style=flat-square&amp;label=quality%20gate" /></a>
  <a href="https://sonarcloud.io/component_measures?id=no1ross_Kaleido&amp;metric=coverage"><img alt="Coverage" src="https://img.shields.io/sonar/coverage/no1ross_Kaleido?server=https%3A%2F%2Fsonarcloud.io&amp;style=flat-square" /></a>
</p>

> [!IMPORTANT]
> Kaleido is under active pre-1.0 development. APIs and package boundaries may change while the public contract is refined. Use the preview for evaluation and experimentation, not as a stable compatibility or production-readiness commitment. It has one maintainer today ([maintainers](MAINTAINERS.md)).

## What is Kaleido?

**Kaleido is a code-first .NET framework for building business services as discoverable queries and executable, stateful steps.**

You implement the business behavior. Kaleido describes the capabilities, validates their declared inputs and process rules, runs the business handlers, records process progress, and returns what can happen next.

Applications use those capabilities through HTTP. A compatible AI agent connects through the **Model Context Protocol (MCP)** adapter and receives the tools, schemas, instructions, lookup details, and continuation guidance needed to interact with them. It does not need a separately programmed, product-specific workflow before connecting.

| Building block | Purpose | Example |
|---|---|---|
| **Queryable** | Publish business information through typed, searchable sources and views. | Find a member or provider. |
| **Process** | Execute business actions with validation, dependencies, availability rules, and resumable state. | Capture a member, select a provider, or submit a request. |
| **Registry** | Describe the available capabilities, their inputs, constraints, relationships, and routes. | Supply discovery metadata for applications and MCP tools for AI. |

**It describes the work. It also runs the work.** Kaleido is not just an API catalog, and it is not an AI model. Its core runtime remains independent of the optional HTTP and MCP adapters.

### What it is for, and what it is not

**Kaleido is for** a contract-first process and query layer over the systems of record you already run: one definition of each business action and query, used the same way by screens, integrations and AI agents. It modernizes the experience over systems that cannot be replaced yet.

| Kaleido is not | Instead |
|---|---|
| A general workflow or orchestration engine | No timers, scheduled waits, sagas or compensation. A process moves when a consumer runs its next step; long-running orchestration stays with a dedicated engine. |
| A system of record | Kaleido keeps the process state (progress, pending questions, who ran what). Business data stays in your systems, behind your handlers and sources. |
| A decision maker | Your handlers decide; Kaleido applies the rules you declare ([ADR 0001](docs/decisions/0001-process-engine-not-decision-maker.md)). |

**Compatibility promise.** During the 1.0 preview any release may break the public API, the wire contracts, error codes or configuration; each break is named in its pull request and the [changelog](CHANGELOG.md), and the public API is recorded so no change goes unnoticed. From 1.0 the packages follow Semantic Versioning: a breaking change to the public API, the HTTP wire contracts, the error codes or the telemetry names needs a new major version. Details: [Versioning, compatibility and support](docs/VERSIONING.md).

## Why Kaleido?

A business process should not have to be implemented again inside every screen, integration, or conversational agent. When both a service and an agent maintain the workflow, a business change requires coordinated implementation, testing, and releases across teams.

Kaleido keeps the business action and process definition in the service. Consumers receive the information needed to use it rather than maintaining another product-specific workflow.

- **Describe once, expose consistently.** Ordinary .NET types and metadata describe queries, actions, inputs, validation constraints, and lookup relationships.
- **Keep progress in the process.** A process identifier and recorded state support continuation without relying on a screen sequence or reconstructing progress from chat history.
- **Make AI a first-class consumer.** The MCP adapter derives its interface from the same capabilities used by other applications, rather than a parallel implementation of the business.
- **Keep business ownership explicit.** Handlers own domain behavior and decisions. Kaleido supplies execution mechanisms and applies the rules you declare.
- **Adopt the parts you need.** HTTP, remote clients, MCP, persistence, and OpenTelemetry remain separate integrations around the core runtime.

### Why the project was started

Kaleido began as a working demonstration of a step concept that years of verbal explanation had not made clear: what an action requires, what it does, and how the process continues. That same explicit model now makes the business usable through AI.

## How it works

### Define the action in the service

A step is a **business action, not a screen or a chat turn**. Its declaration describes the action and its inputs; a separate handler implements what the action does.

This excerpt from the [PriorAuth sample](https://github.com/no1ross/Kaleido-Samples-PriorAuth) shows where the interaction information starts. Namespace imports and the enrollment and date inputs are omitted; this is not the complete class or a runnable service.

```csharp
[ProcessStep(
    DisplayName = "Intake - Capture Member",
    Description = "Records member information against the intake session for auditing and correlation.")]
[AvailableAfter<ValidateMemberStep>]
public sealed record CaptureMemberStep : IProcessStep
{
    [Required]
    [Description("The validated member: the same Member Search result the caller picked for Validate Member.")]
    [Display(
        Name = "Member",
        Prompt = "What is the member's ID, first and last name, and date of birth?")]
    [Lookup(
        Service = "member",
        Source = nameof(MemberQueryContextSource),
        View = nameof(MemberSearchViewSource),
        Field = "MemberId",
        Display = ["DisplayName", "DateOfBirth", "MemberNumber",
                   "PlanName", "EffectiveDate", "TerminationDate"])]
    public required Guid MemberId { get; init; }

    // MemberEnrollmentId and DateOfService are omitted from this excerpt.
}
```

| Declaration | Meaning |
|---|---|
| `ProcessStep` | Describes the action and its purpose; the type's name is its identity. |
| `AvailableAfter<ValidateMemberStep>` | Makes completion of validation an availability condition; it does not automatically execute this step or make it the required next step. |
| `Required` and the property type | Declare required input and its shape. |
| `Display` / `Description` | Supply the input's label, prompt, and explanation. |
| `Lookup` | Identifies the query and result field supplying the value, plus the fields used to help a person recognize the record. |

**This is service code, not agent code.** Kaleido publishes the registered metadata as tool descriptions and schemas; the agent does not read this C# file. The business handler still performs the action and validates its meaning. Descriptions and lookup guidance do not replace those checks.

### Execute and return the next work

After the application's queries, steps, and handlers are registered, a normal step execution follows this lifecycle:

1. **Load context.** Create a process for an initial step or reload an existing process by its identifier.
2. **Check the request.** Validate the inputs and check the declared process relationships against recorded progress.
3. **Run the handler.** Invoke the application's business implementation.
4. **Record progress.** Save the outcome and updated framework process state.
5. **Return continuation.** Explain what happened and what work is required, available, or waiting for information.

The response can require a step, offer available choices, request information, or signal a handoff. The consumer follows that current guidance, not a fixed screen sequence.

Kaleido is a [process engine, not a decision maker](docs/decisions/0001-process-engine-not-decision-maker.md): business policy and handler outcomes remain in your code; the framework applies declared rules rather than choosing a business path for the user.

## AI-native through MCP

**Connect the agent. Kaleido supplies the product-specific operating context.**

[`Kaleido.Mcp`](src/Mcp/Mcp/README.md) translates the caller-visible central registry into tools and input schemas. Descriptions and lookups explain how to supply inputs; results describe how to continue.

When more information is needed, the service supplies an [information request](docs/INFORMATION_REQUESTS.md). The adapter presents forms where supported or returns guidance for the agent to ask the user. The business owns the questions and the meaning of the answers.

### Details arrive when they are needed

The initial tool list does not have to enumerate the entire workflow. In **FrontDoor** listing mode, the starting catalog exposes entry capabilities and helpers. Subsequent `next` responses can describe additional actions, their inputs and lookups, and how to invoke them through `call_tool`.

For example, validation can return Capture Member in `next.required`, with its invocation details, even though it was not on the starting tool list. The process supplies the interface needed to continue as it runs.

The agent needs generic MCP/tool-use support, not a separately implemented product workflow. Kaleido does not provide model inference, train the agent, or guarantee every model's behavior.

## Change workflows and add products without a second agent project

### Change the service, not a duplicate workflow inside the agent

Change a question, step relationship, or continuation within the supported contracts, and the service supplies the updated information. A generic agent integration does not need a matching product-workflow implementation change.

That removes a duplicate implementation and a cross-team release dependency, not the responsibility to test the changed experience.

### Add products, not agent integrations

A company can bring one of five products onto conversational AI, then add another the following month. Build the second product's Kaleido service, deploy its versioned gateway route and access rules, and register its capabilities centrally. The existing MCP integration can expose it without adding a product-specific workflow inside the agent.

**Same agent. Same MCP connection. Another product available.**

Registry hydration picks up the new service version; callers use its published capability URLs through the gateway. Product implementation, versioned routing, access configuration, and validation still happen. A new client feature or breaking contract change can still require coordination.

## Quick start

Reference the relevant projects, or matching preview packages from your available package feed. The examples below show host registration, not a complete business application; add the appropriate namespace imports and your own query, step, and handler implementations.

> [!WARNING]
> These examples are for local evaluation and explicitly disable capability authorization. Do not expose them as production services. Configure host authentication, capability authorization, and record-level access for your deployment. An authenticated MCP host also needs a handler that exchanges the caller's token for downstream calls.

### Publish a business service over HTTP

Run a separate [control registry](src/Registry/Registry/README.md) and configure `Kaleido:BaseUrl`
with the gateway's address. Map `/registry` to the registry host and `/orders/1` to this service's
host in the gateway's deployment configuration. The registry never holds instance addresses.

```csharp
var builder = WebApplication.CreateBuilder(args);

var kaleido = builder.Services
    .AddKaleido(builder.Configuration, new()
    {
        ServiceName = "orders",
        ServiceVersion = "1",
        DisplayName = "Orders",
        Assemblies = new[] { typeof(Program).Assembly },
        AuthorizationMode = KaleidoAuthorizationMode.None, // Local evaluation only.
    })
    .AddInMemoryStores() // Local evaluation only: process state is lost on restart.
    .AddHttp()
    .AddControlRegistryClient();

var app = builder.Build();

app.MapKaleidoHttp();

app.Run();
```

Kaleido discovers sources, views, steps, and handlers from the configured assemblies and registers their versioned HTTP URLs with the central registry. `MapKaleidoHttp()` requires a central-registry source: business services register one with
`AddControlRegistryClient()`, and a control-registry host has its own via `AddControlRegistry()`.
`ServiceVersion` is required for service endpoints.

`Assemblies` is required even for client-only hosts. A missing or empty list fails at `AddKaleido()` with `missing_assembly`; there is no implicit calling-assembly scan.

### Expose registered services through an MCP host

In a **separate MCP host**, point `Kaleido:BaseUrl` at the gateway, which maps `/registry` to the
registry host and each `/{service}/{version}` path to its deployed service. The MCP host needs no
per-service client list:

```json
{
  "Kaleido": {
    "BaseUrl": "http://localhost:5100",
    "OnBehalfOfCalls": "Exchange"
  }
}
```

Register central participation and the one outbound gateway client before adding MCP:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddKaleido(builder.Configuration, new()
    {
        ServiceName = "mcp-reader",
        ServiceVersion = "1",
        Assemblies = new[] { typeof(Program).Assembly },
        AuthorizationMode = KaleidoAuthorizationMode.None, // Local evaluation only.
    })
    .AddInMemoryStores() // Local evaluation only.
    .AddHttp()
    .AddHttpClients()
    .AddControlRegistryClient()
    .AddMcp(new() { ServerName = "business" });

var app = builder.Build();
app.MapKaleidoMcp("/mcp");
app.Run();
```

Connect an MCP-compatible agent to `/mcp`. `AddHttpClients()` sends to the gateway and resolves
capability URLs from the host's hydrated central registry; `AddHttpClients()` rejects nonempty
`Kaleido:Clients` entries at registration.
`AddMcp()` requires `Exchange`: the agent's user's token cannot be passed unchanged to another
service. With authentication, add an `AddKaleidoClientHandler<THandler>()` that exchanges it
([which token a service sends](docs/guide/callers-and-correlation.md#which-token-a-service-sends),
#574). This local-only example has no sign-in. Use `https` outside local development and configure
the gateway's per-version destinations in the same deployment as each service.

See the [HTTP client](src/Http/Http.Client/README.md) and [MCP adapter](src/Mcp/Mcp/README.md) guides for configuration details, and the [consumer guide](docs/guide/README.md) for a complete service built step by step.

### Choose process storage

Every store a service uses is chosen explicitly: a service with process steps keeps process state and idempotency records, and one that publishes events through Kaleido's outbox keeps an outbox. A store left at its in-memory default stops the host at startup with `stores_not_chosen` ([ADR 0028](docs/decisions/0028-stores-are-chosen-explicitly.md)); `AddInMemoryStores()` above is that choice for local evaluation (nothing survives a restart, nothing is shared between instances). For local persistence, use the SQLite reference provider instead, in the business-service registration **before `builder.Build()`**:

```csharp
kaleido
    .AddSqliteContextStore("Data Source=kaleido-process.db")
    .AddSqliteOutboxStore("Data Source=kaleido-process.db")
    .AddSqliteIdempotencyStore("Data Source=kaleido-process.db");
```

[`Kaleido.Provider.SQLite`](src/Providers/Provider.SQLite/README.md) is a **reference implementation for samples and local development**, not the production-provider recommendation. Registration does not create the database: the host creates the schema once at startup (`SqliteKaleidoContextDbContext.Database.EnsureCreatedAsync()`, shown in the [provider README](src/Providers/Provider.SQLite/README.md#using-the-example-implementation)), and `EnsureCreated` never migrates an existing database.

Production deployments provide and validate stores suited to their infrastructure: `IKaleidoContextStore` (process state), `IKaleidoOutboxStore` (every event the service writes, until the relay publishes it), `IKaleidoTransactions` (state and events atomic on one database) and `IKaleidoIdempotencyStore` (requests sent with an idempotency key). Every record is scoped by the service and its version, so services may share one database, and one host per outbox database runs the relay (`AddOutboxRelay<TPublisher>()`) - see [ADR 0036](docs/decisions/0036-stores-scoped-by-service-and-a-shared-outbox-relay.md).

## The capability model

### Queryable: what information does the business know?

A source publishes business data; its query context describes search, filter, and sort capabilities. Views provide supported projections and parameters. Metadata describes fields, types, constraints, and paging. Queries read information; steps perform actions.

### Process: what can the business do?

Steps declare inputs and relationships; handlers implement behavior. The process has its own identity and current state. An authorized consumer reads that state to continue work rather than treating the conversation transcript as the process record.

### Discovery: how does a consumer find it?

The central registry (`GET /registry` on the registry host) publishes every service version's capabilities, status and logical gateway routes. Services push their own registrations; each participant hydrates the unfiltered copy for calls, while caller-facing discovery is authorization-filtered. MCP exposes tools from that caller-visible model. Discovery never replaces execution-time validation or authorization.

## Deployment responsibilities

The application and host retain business authority: authentication, authorization, downstream credentials, record-level access, domain validation, and required approvals. A `processId` identifies work; it is not proof of permission. Tool descriptions, prompts, lookups, and confirmation hints guide interaction; they do not enforce permission or prove human consent.

Validate production storage, recovery, privacy, and retry behavior for the deployment. Running several instances is covered in the guide's [deployment page](docs/guide/deployment.md#several-instances). Business side effects and framework-state persistence are not automatically one transaction; Kaleido does not promise exactly-once execution of arbitrary external effects.

## Packages

| Package | Purpose |
|---|---|
| [`Kaleido`](src/Kaleido) | Transport-free Process and Queryable runtimes and store contracts |
| [`Kaleido.Platform`](src/Platform) | Shared registry, Process and Queryable contracts, headers and JSON conventions |
| [`Kaleido.Server`](src/Server) | Shared ASP.NET Core authorization, caller and endpoint services |
| [`Kaleido.Client`](src/Client) | Neutral clients, registry participation and transport choice |
| [`Kaleido.Http`](src/Http/Http) | Shared HTTP route and URL vocabulary |
| [`Kaleido.Http.Server`](src/Http/Http.Server) | Service HTTP endpoints and execution adapters |
| [`Kaleido.Http.Client`](src/Http/Http.Client) | Registry-resolved calls over one gateway HTTP client |
| [`Kaleido.Registry`](src/Registry/Registry) | Control registry, store contract, leases, views and HybridCache |
| [`Kaleido.Registry.Http`](src/Registry/Registry.Http) | Control-registry HTTP endpoints |
| [`Kaleido.Mcp`](src/Mcp/Mcp) | Caller-visible registry-derived MCP tools and guided continuation |
| [`Kaleido.Provider.SQLite`](src/Providers/Provider.SQLite) | Reference process, outbox, idempotency and registry stores |
| [`Kaleido.Observability.OpenTelemetry`](src/Observability/Observability.OpenTelemetry) | Opt-in telemetry export |
| [`Kaleido.Analyzers`](tools/analyzers/Kaleido.Analyzers) | Consumer compile-time checks (`KAL2xxx`), included by `Kaleido` |

The [`Kaleido.Grpc` projects](src/Grpc/Grpc/README.md) and
[`Kaleido.Registry.Grpc`](src/Registry/Registry.Grpc/README.md) are non-packable placeholders; gRPC
is not implemented yet.

### Prerequisites

- **Using the packages:** they target `net8.0`, so a service runs on .NET 8 or later; run it on a supported runtime (.NET 10 after .NET 8 reaches end of support on 10 November 2026) and override the 8.x dependencies to patched versions ([support window](docs/VERSIONING.md#support-window)). `Kaleido.Http.Server`, `Kaleido.Registry.Http` and `Kaleido.Mcp` need ASP.NET Core (`Microsoft.AspNetCore.App`). Trimmed and NativeAOT publishing are not supported ([deployment modes](docs/VERSIONING.md#deployment-modes)).
- **Building this repository:** the .NET 10 SDK, pinned in `global.json` (#578).

### Install

The packages are previews. Tagged versions are published to **nuget.org**, so no extra source is needed ([releasing](RELEASING.md)); add what the service needs:

```bash
dotnet add package Kaleido --prerelease                              # core runtimes
dotnet add package Kaleido.Http.Server --prerelease                  # publish HTTP capabilities
dotnet add package Kaleido.Http.Client --prerelease                  # central-registry and gateway HTTP clients
dotnet add package Kaleido.Registry.Http --prerelease                # host the control registry over HTTP
dotnet add package Kaleido.Mcp --prerelease                          # expose registry capabilities to agents
dotnet add package Kaleido.Platform --prerelease                     # shared contracts only (when needed)
dotnet add package Kaleido.Observability.OpenTelemetry --prerelease  # logs, traces and metrics over OTLP
dotnet add package Kaleido.Provider.SQLite --prerelease             # reference store (local development)
```

HTTP adapters bring their required `Kaleido.Http`, `Kaleido.Server` or `Kaleido.Client`, `Kaleido.Platform` and core dependencies transitively. The packages move together: upgrade all Kaleido packages a project references at once ([versioning](docs/VERSIONING.md#versions)).

Preview builds between tags are published only to the repository's GitHub Packages feed, `https://nuget.pkg.github.com/no1ross/index.json`; to use one, add that feed as a source with a GitHub token that can read packages:

```bash
dotnet nuget add source https://nuget.pkg.github.com/no1ross/index.json --name kaleido --username <github-user> --password <token-with-read:packages>
```

## Documentation

- **[Consumer guide](docs/guide/README.md)**: how Kaleido works end to end and how to choose its options, with compiled, tested examples. Start here.
- [Architecture](ARCHITECTURE.md)
- [Patterns: steps, handlers, sources, views, lookups](docs/PATTERNS.md)
- [Core runtime](src/Kaleido/README.md)
- [HTTP transport](src/Http/Http.Server/README.md) and [contracts](src/Platform/README.md)
- [HTTP clients](src/Http/Http.Client/README.md)
- [MCP adapter](src/Mcp/Mcp/README.md)
- [Information requests](docs/INFORMATION_REQUESTS.md)
- [Authorization](docs/AUTHORIZATION.md)
- [Error codes](docs/ERROR_CODES.md)
- [Correlation](docs/CORRELATION.md)
- [Analyzers (KAL rules)](docs/ANALYZERS.md)
- [Versioning, compatibility and support](docs/VERSIONING.md)
- [Decision records](docs/decisions/README.md), starting with [process engine, not decision maker](docs/decisions/0001-process-engine-not-decision-maker.md)
- [SQLite reference provider](src/Providers/Provider.SQLite/README.md)
- [OpenTelemetry provider](src/Observability/Observability.OpenTelemetry/README.md)
- [Prior authorization sample](https://github.com/no1ross/Kaleido-Samples-PriorAuth) and [e-commerce sample](https://github.com/no1ross/Kaleido-Samples-ECommerce), each in its own repository

## Project status

Kaleido is being prepared for its first public release. The current focus is refining the public API, strengthening and validating execution and authorization behavior, validating package composition, and keeping documentation aligned with the implementation.

The MCP adapter is implemented, not merely a planned AI integration. That does not make the preview a production-readiness certification. Use preview releases for evaluation and experimentation; what holds during the preview, and what 1.0 decides, is in [Versioning, compatibility and support](docs/VERSIONING.md).

## Contributing

Issues, design feedback, and focused pull requests are welcome: see [CONTRIBUTING.md](CONTRIBUTING.md) (setup, the per-commit gate, pull requests) and [SUPPORT.md](SUPPORT.md) for questions. Start with [AGENTS.md](AGENTS.md) and [ARCHITECTURE.md](ARCHITECTURE.md) before making structural changes. Everyone follows the [code of conduct](CODE_OF_CONDUCT.md).

## Security

Report vulnerabilities privately, never in a public issue: see [SECURITY.md](SECURITY.md).

## License

Kaleido is licensed under the [Apache License 2.0](LICENSE).
