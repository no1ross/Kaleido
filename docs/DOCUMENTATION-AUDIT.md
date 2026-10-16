# Documentation audit

Issue: [#353](https://github.com/no1ross/Kaleido/issues/353). The test applied to every document:
*if a developer learned Kaleido only from these docs and samples, where would the framework
surprise them?* Each claim (type and member names, signatures, routes, configuration keys,
defaults, error codes, behavior) was checked against the production code; each important
consumer-facing behavior was checked for a place in the docs where its audience would look.

Out of scope: `docs/archive/` and `docs/published/` (historical artifacts).

## 1. Documentation reviewed

- Root: `README.md`, `ARCHITECTURE.md`, `AGENTS.md`, `.github/copilot-instructions.md`.
- `docs/`: `PATTERNS.md`, `INFORMATION_REQUESTS.md`, `ERROR_CODES.md`, `AUTHORIZATION.md`,
  `CORRELATION.md`, `ANALYZERS.md`, `ASSETS.md`, `BRAND.md`, `PRERELEASE_PROMPT.md`,
  `decisions/README.md` and ADRs 0001-0016 (for contradictions with current code only; ADRs are
  records and were not rewritten).
- `src/`: `AGENTS.md`, `ARCHITECTURE.md`, every project README, the MCP adapter's agent
  instructions (`Instructions/*.md`), package metadata in every `src/*/*.csproj`, and the XML
  documentation of the consumer-facing public API.
- `tools/analyzers/README.md` and the three `AnalyzerReleases.Unshipped.md` files, against every
  `DiagnosticDescriptor`.
- `tests/AGENTS.md`, `tests/FUNCTIONAL-TEST-AUDIT.md`, `.devin/skills/priorauth-login/SKILL.md`.
- Samples as executable documentation: `samples/PriorAuth` (docs, every host, shared code),
  `samples/ECommerce`, `samples/SQLite`, both UI READMEs.
- Every relative link and anchor in every Markdown file (scripted check: 231 links, all resolve).

## 2. Accurate as found

`.github/copilot-instructions.md`; `docs/CORRELATION.md` (every header, trust rule, sanitize
limit and echo rule verified); `docs/ASSETS.md`; `docs/BRAND.md`; `docs/decisions/README.md`
(index, statuses, ADR 0011 marked superseded by 0016) and ADRs 0009, 0015, 0016;
`docs/ERROR_CODES.md` code tables for `ProcessorErrorCodes`, `QueryableErrorCodes`,
`ConfigurationErrorCodes`, `HttpClientErrorCodes`, `SqliteErrorCodes` (every constant 1:1);
`tools/analyzers/README.md`; the consumer and source `AnalyzerReleases.Unshipped.md` files;
`tests/FUNCTIONAL-TEST-AUDIT.md`; the `priorauth-login` skill; the Abstractions README sections on
JSON enum values and paging/`totalCount`; the root README quick start (except the storage note,
below).

## 3. Documentation -> code mismatches (fixed)

| Where | What the docs said | What the code does (evidence) |
|---|---|---|
| `src/Kaleido.Http/README.md` | `app.MapProcessor().RequireAuthorization(...)` / `MapQueryable()` for per-surface policies | Both are `internal` (`ProcessorEndpointRouteBuilderExtensions.cs:19`, `QueryableEndpointRouteBuilderExtensions.cs:26`); the example could not compile. Only `MapKaleidoHttp()` is public. Same claim removed from root `ARCHITECTURE.md`. |
| `src/Kaleido.Provider.SQLite/README.md`, `UseSqliteKaleidoContextStore` XML docs | the store creates its schema with `EnsureCreated` | Nothing in the provider calls `EnsureCreated`; the host must (`samples/PriorAuth/Services/Intake/Program.cs`). Following the README gave "no such table" on the first save. The README now shows the host step; the root README states it. |
| `src/Kaleido.Http.Client/README.md`, `src/ARCHITECTURE.md` | `KaleidoQueryableClientException`, `KaleidoQueryableClientServiceCollectionExtensions` | Neither exists; both clients throw `KaleidoHttpClientException`; registration is in `KaleidoClientServiceCollectionExtensions.cs`. |
| `src/Kaleido.Http.Client/README.md` | header `X-Kaleido-ProcessId` | `X-Kaleido-Process-Id` (`KaleidoCorrelationHeaders.cs`). |
| `src/ARCHITECTURE.md` | `CountAsync` runs only when a page is explicitly requested; otherwise `TotalCount = items.Count` | A pageable capability always pages; a partial page totals `offset + count` (`QueryContextEngine.TotalCountAsync`). Contradicted `src/AGENTS.md`. |
| `src/Kaleido/README.md`, `src/ARCHITECTURE.md`, `src/AGENTS.md`, root `AGENTS.md`/`ARCHITECTURE.md` | `DataTypeMapper`, `NullEventPublisher`, `ExecutionProcessor`, `ProcessExecutor`, `ProcessRuntime`, `ValidationException`, `KaleidoEnumConverter`, `ValueConverter`, `IKaleidoProcessClientFactory`, `KaleidoProcessClient`, `AddProcessClient`, `ProcessStepInfo`, `QueryErrorResponse` | None exist. Replaced with `TypeDescriber`, the internal no-op `EventPublisher`, `ProcessorExecutor`, `ProcessorRuntime`, `ProcessorStepInvoker`, `KaleidoValidationException`, `IKaleidoProcessorClientFactory`, `KaleidoProcessorClient`, `AddProcessorClient`, `ProcessRequiredStep`, `KaleidoErrorResponse`. |
| root `ARCHITECTURE.md` | `AddKaleido(IConfiguration, Action<KaleidoServiceOptions>)`; "scans for `[ProcessStep]` types"; queryable dispatch "delegated-view" | `Action<KaleidoServiceOptionsBuilder>`; steps are discovered by `IProcessStep` (`ProcessorServiceCollectionExtensions.cs:168`); delegated sources have no views. |
| root `ARCHITECTURE.md`, `src/Kaleido.Http/README.md` | per-step / per-context metadata endpoints | Removed earlier; step and query metadata come from the registry. |
| `docs/AUTHORIZATION.md` | `new AuthorizationMetadata("staff", [])` as a role default | The first parameter is `Policy` (`AuthorizationMetadata(string? Policy, IReadOnlyList<string> Roles)`); the example set a policy named "staff". |
| `docs/ERROR_CODES.md` | `unsupported_data_type` / `data_conversion_error` raised by `DataTypeMapper` | Defined in `FrameworkErrorCodes` but raised nowhere; marked reserved (see 11). |
| `src/Kaleido.Http.Mcp/Instructions/FrontDoorInstructions.md` (sent to agents) | `lookup.search` | The `next` payload has `lookups`, each with a `search` (`KaleidoToolInvoker.FrontDoor.cs`). |
| `src/Kaleido.Http.Mcp/Instructions/DefaultInstructions.md` (sent to agents) | a user-facing refusal reason is any code but `qry_*`, `pro_*`, `argument_error` | The adapter also excludes `httpclient_*`, `mcp_*`, `framework_error` (`KaleidoToolInvoker.cs` `ServiceReasons`). |
| `src/Kaleido.Http.Mcp/README.md` | missing-description warnings "at startup"; session clients get elicitation | Logged when the tools are built (first request, each registry change); session elicitation corrected earlier (#350). |
| `src/Kaleido.Provider.SQLite/README.md` | the in-memory store warns "at registration" | It warns when created (first resolution; `InMemoryKaleidoContextStore` constructor). |
| `docs/ANALYZERS.md` | KAL0010 also reports unused constructor parameters | The analyzer has only the readonly-field descriptor (`DependencyRetentionAnalyzer.cs`; unused parameters are IDE0060). KAL0002's allow-list also covers `FormatException` and MCP's `InputRequiredException`. |
| `tools/analyzers/Kaleido.Analyzers.Testing/AnalyzerReleases.Unshipped.md` | KAL1012 missing; KAL1006 severity `Disabled` | KAL1012 exists (Error); KAL1006's descriptor severity is Error (the per-project disable lives in `.editorconfig`). |
| `tests/AGENTS.md` | `ProcessAspNetCoreFixture` | The class is `ProcessorAspNetCoreFixture`. |
| `samples/PriorAuth/AGENTS.md` | handoff calls `/processes/execute` with the original payload; `*.Artifacts/` projects; a "smaller compose file"; ports labelled `providersearch`, `memberservice` | The handler calls the entry step with a typed payload through the process client (`CaptureRequestedServiceHandler.cs`); no `*.Artifacts`; one compose file; the compose services are `provider` and `member`. |
| `src/Kaleido.Http.Abstractions/README.md`, `src/Kaleido.Http.Client/README.md` | "Where to look" paths `Process/Contracts/`, `Processor/IKaleidoProcessorClient.cs` (in the client) | Real paths: `Processor/ProcessContracts.cs`, `Queryable/QueryableContracts.cs`, client interfaces in `Kaleido.Http.Abstractions`. |
| XML docs | `[KaleidoAuthorization]` applies to "query context"; `TypeFilter` filters "process steps"; `HttpClientErrorCodes` are `pro_`/`qry_` prefixed; `AggregatedRegistryResponse` refers to internal `AddProcessorClient()` | Sources (never contexts); steps and query sources/views; `httpclient_*` plus `missing_base_url`; `AddHttpClients()`. |
| `KaleidoServiceOptions` error message, several READMEs | `o.ServiceName = "my-service"` | KAL2005 warns on separators; examples now use `myservice`. |

## 4. Code -> documentation gaps (documented)

- **`[Lookup]`** had no consumer documentation (only ADR 0016): new section in `docs/PATTERNS.md`.
- **`KaleidoServiceOptions`** members `DisplayName`, `Description`, `IsEntryProcessor`,
  `InstanceId`, `TypeFilter`: listed in `src/Kaleido/README.md`.
- **Default no-op event publisher logs a Warning** that events are discarded; **the in-memory
  store logs a Warning** that state is lost: stated where they are described.
- **MCP:** startup failures `mcp_missing_http`, `mcp_missing_clients`,
  `mcp_missing_instructions_file` and that `AddHttp()`/`AddHttpClients()` must come **before**
  `AddMcp()`; `mcp_no_pending_information_request`; built-in process tools exist only when the host
  has processes; tool naming (suffix stripping, 64-character hash shortening, KAL2019/KAL2020).
- **HTTP:** `RegistryDownstreamMaxParallelism`; `IKaleidoRegistryService` for in-host consumers;
  `destructive`/`external`/`requiresConfirmation` on `requiredStep`; public `ProcessEndpointNames`
  and `RegistryEndpointNames` vs internal URL helpers.
- **Client:** `TryGetClientForService`; the untyped `ExecuteStepAsync(stepName, json, processId)`;
  caller/process forwarding exists only when clients are configured.
- **Contracts:** `QueryableSourceResponse.Name` (what a client passes) and its descriptive fields;
  errors use `KaleidoErrorResponse`.
- **Errors:** `KaleidoAuthorizationException` and `KaleidoErrorCodes` (`argument_error`,
  `framework_error`) in `docs/ERROR_CODES.md`; pointer to the MCP codes.
- **Information requests:** `proposedAnswers` and their validation.
- **Authorization:** a view without its own attribute takes its source's rule
  (`QueryableRegistry.cs`).
- **OpenTelemetry:** the `configureTracing`/`configureMetrics` hooks; the known configuration
  limitation (#354).
- **Analyzers:** KAL2019 and KAL2020 were shipped but undocumented.
- **Front door:** the root README now links authorization, error codes, correlation, analyzers,
  patterns, contracts and the decision records.

## 5. Outdated APIs, concepts, terminology

The vocabulary drift was mostly pre-rename names: `Process*` where the code says `Processor*`
(`ProcessExecutor`, `ProcessRuntime`, `IKaleidoProcessClientFactory`, `AddProcessClient`), the
removed metadata/catalog endpoints, `DataTypeMapper` (now `TypeDescriber`), `NullEventPublisher`,
the queryable "context source"/"delegated view" model, and `ProcessStepInfo`. All current
references were updated (section 3). "Process" (an instance) vs "Processor" (owns steps) was kept
distinct.

## 6. Examples and samples corrected

- `src/Kaleido.Http/README.md`: removed the non-compiling per-surface authorization example.
- `src/Kaleido.Provider.SQLite/README.md`: the usage example creates the schema.
- `docs/AUTHORIZATION.md`: `DefaultAuthorization` example uses named arguments for a role.
- `src/Kaleido.Http.Client/README.md`: added the untyped step call.
- `samples/SQLite`: set the missing `ServiceName`. The sample failed at startup
  (`invalid_service_name`); it now starts and serves its registry. Its docs now say what it is: a
  queryable host over CSV records, not a SQLite-store sample.
- PriorAuth hosts (9): a missing connection string threw Kaleido's `InvalidServiceName`; they now
  throw the sample's own `PriorAuthErrorCodes.MissingConnectionString`.
- PriorAuth Radiology: removed a debug `Console.WriteLine` from a step handler.
- ECommerce: a handler threw `InvalidOperationException` (now `KaleidoFrameworkException`); a
  catch-all that only rethrew was removed from the seeder.
- `samples/PriorAuth/priorauth-ui/README.md`: a sample section above the Angular boilerplate.

## 7. Architecture documentation corrected

Root `AGENTS.md`/`ARCHITECTURE.md` and `src/AGENTS.md` described six projects; there are seven.
`Kaleido.Http.Mcp` (depends on `Kaleido.Http` + `Kaleido.Http.Client`) was added to every project
list, boundary section, registration step, test list and rule of thumb. The test list gained the MCP
and OpenTelemetry suites; the sample list gained ECommerce and the CSV queryable sample. The
`IKaleidoContextStore` contract is placed in `Kaleido` (`Storage/`), not in the SQLite provider.
Project dependencies were checked against every `ProjectReference`.

## 8. Duplication and contradictions resolved

- `TotalCount` semantics: `src/ARCHITECTURE.md` contradicted `src/AGENTS.md` and the code; both now
  state the code's rule, and the Abstractions README holds the consumer-facing table.
- Root `AGENTS.md` vs `src/AGENTS.md` (`DataTypeMapper` vs `TypeDescriber`) aligned.
- MCP error codes are owned by the MCP README; `docs/ERROR_CODES.md` points to it instead of
  copying the table.
- The SQLite schema step is explained once (provider README) and referenced from the root README.
- `samples/PriorAuth/AGENTS.md` handoff text now agrees with `HANDOFF.md` and the code.

## 9. Documentation added

`docs/PATTERNS.md` "Lookup inputs"; `KaleidoErrorCodes` section and `KaleidoAuthorizationException`
row in `docs/ERROR_CODES.md`; MCP setup requirements and naming rules; OpenTelemetry "Adding
instrumentation" and known limitation; `KaleidoServiceOptions` list; registry in-host access; the
PriorAuth UI section; this audit.

## 10. Documentation removed as obsolete

The per-surface authorization example; references to removed endpoints, types and files (section
3); the `*.Artifacts` and "smaller compose file" lines; the duplicated client exception
explanations.

## 11. Ambiguities (not resolved by inventing a rule)

- **OpenTelemetry configuration** ([#354](https://github.com/no1ross/Kaleido/issues/354), bug):
  `AddOpenTelemetry()` applies only `OTEL_*` keys and overrides the service name; the PriorAuth
  `OpenTelemetry` sections are not applied by it. Fixed separately (last commit of this PR).
- **`unsupported_data_type`, `data_conversion_error`**: public constants that nothing raises. Kept
  and marked reserved; remove or use them deliberately.
- **Route-level role matching** is ASP.NET's (`RequireRole`), while the in-handler evaluator compares
  roles case-insensitively (`KaleidoAuthorizationEvaluator.cs`); the docs do not promise either
  casing.
- **`InitialSteps[].executeUrl` is empty** in `ProcessorRegistryResponse` while `Steps` carry URLs;
  intent unclear, not documented either way.
- **Caller roles** are read from `ClaimTypes.Role` only; a host issuing `roles` claims gets none.
  Possibly intended; flagged.
- **PriorAuth `ProcessorMappings`** in Intake `appsettings.json` is read by no code and `HANDOFF.md`
  says the mapping is not there; left for the owner.
- **`HANDOFF.md` / `AGENTS.md` tracker codes** (`HP-025`, `HP-026`, `#209`) whose status could not be
  verified.
- **ECommerce** depends on a committed SQLite database (the initializer call is commented out), and
  `OrderQueryContext` publishes no query rules; intent unclear.
- **MCP session elicitation** over the stateless endpoint (#350, open on request).

## 12. Intentionally not documented

Internal plumbing with no consumer decision attached: registry caches and snapshot keys beyond what
is documented, `RegistryUrlValidator`, correlation stampers, catalog cache sizes, search-tool
ranking limits, opaque MCP continuation-state formats, resilience internals, `NoRetry`, the
`Kaleido.Testing` harness, KAL ids skipped in numbering. The XML documentation gap on 526 public
members (CS1591 is suppressed globally) is tracked in
[#355](https://github.com/no1ross/Kaleido/issues/355) rather than documented member by member here.
