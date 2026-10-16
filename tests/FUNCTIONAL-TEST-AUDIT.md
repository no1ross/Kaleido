# Functional-test audit

Issue: [#347](https://github.com/no1ross/Kaleido/issues/347). The question behind every entry:
*if someone changes something meaningful in Kaleido and breaks consumer-visible behavior, which test
fails?* Unit tests (one class per SUT, mocked collaborators) prove classes in isolation; the suites
below prove that the real components agree with each other.

Suites: `Kaleido.Http.FunctionalTests` (TestServer hosts, real runtime), `Kaleido.Http.Mcp.FunctionalTests`
(SDK MCP client against a real Kaleido host), `Kaleido.IntegrationTests` (DI and EF Core).

## How gaps were verified

For each area, realistic production mutations were listed (reversed conditions, dropped
registrations, ignored metadata, wrong mapping, broken round trips). A mutation counts as a gap when
the whole suite stayed green. After the new tests were written, the key mutations were applied to the
production code one at a time, the suites run, and the file restored:

| Mutation | Caught by |
|---|---|
| `CompiledQueryApplier`: And/Or groups inverted | `QueryContractTests` (Or, And, nested, forwarded) |
| `CompiledQueryApplier`: `NotEquals` not negated | `QueryContractTests.AFilterCondition_*(notequals)` |
| `CompiledQueryApplier`: `Contains` -> `StartsWith` | `QueryContractTests.AFilterCondition_*(contains "amma")` |
| `QueryContextEngine`: view paging inherited from the source | `QueryContractTests.AView_*` (3 tests) |
| `QueryRequestCompiler`: sort `sequence` ignored | `QueryContractTests.AnExplicitSortSequence_*` |
| `ToApiBody`: nested filter groups dropped | `QueryContractTests.AForwardedQuery_*` |
| `StepAvailabilityResolver`: `[AvailableUntil]` ignored | `ProcessBehaviorTests.ARepeatableStep_*AvailableUntil*` |
| `StepAvailabilityResolver`: `[Repeatable]` ignored | `ProcessBehaviorTests`, `InformationRequestEndpointTests` |
| `ProcessorExecutor`: a rejected step saved like a completed one | `ProcessBehaviorTests.ARejectedStep_*` |
| `KaleidoExceptionHandler`: concurrency conflict -> 500 | `ProcessBehaviorTests.TwoRequestsForTheSameProcessAtOnce_*` |
| `ProcessorRuntime`: an unknown id "handed off" by this service itself | `ProcessBehaviorTests.AnUnknownProcessId_FromThisServiceItself_*` |
| `KaleidoRegistryService`: downstream processes not merged | `RegistryAggregationTests` |
| MCP: several query filters or-ed instead of and-ed | `McpContractTests.AQueryTool_*` |
| MCP: user-facing text marked for the assistant | `McpContractTests.ABusinessMessage_*`, `ARejectedStep_*` |
| MCP: MRTR request state not round-tripped | `McpContractTests.WithMrtr_*`, `WhenTheUserDeclines_*`, `McpEndpointTests.WithElicitation_*` |

Before this work, every row except "Repeatable ignored" (partially) left the suite green.

## 1. Major functional flows

- **Queryable:** direct source query, view query, delegated source query, async source and view,
  registry/discovery, typed client, delegated forwarding (`ToApiBody` round trip), EF Core execution.
- **Process:** single-step execute (new / resumed), multi-step execute, state read, information
  request round trip, handoff (out and in), unknown process id, idempotent retry, event outbox,
  registry/discovery, typed client, rejection, unexpected failure, optimistic concurrency.
- **Cross-cutting:** inbound correlation echo, outbound correlation propagation, error shape and
  status mapping, registry endpoint and aggregation, per-caller filtering, endpoint authorization,
  caller forwarding, client resilience, health checks, bootstrap composition.
- **MCP:** tools/list from the registry, query tools, step tools, information requests (MRTR and
  agent-asked), state/answer tools, front door, caller-token forwarding, correlation, sign-in,
  downstream unavailable.

## 2. Already adequately protected

Information requests over HTTP (`InformationRequestEndpointTests`), idempotency on the step
endpoint (`IdempotentExecutionTests`, real SQLite), event outbox delivery/retry/multi-host and query
redaction (`EventOutboxTests`), endpoint authorization in Authenticated mode
(`AuthorizationEndpointTests`), correlation echo on success (`CorrelationEchoTests`,
`CorrelationIdentityTrustTests`), registry ETag/304/strict-on-failure (`RegistryEndpointTests`),
caller forwarding order (`CallerForwardingTests`), delegated paging pass-through
(`DelegatedQuerySourceTests`), MCP tool listing/hints/caller forwarding/front door
(`McpEndpointTests`, `McpFrontDoorTests`).

## 3. Weak or missing flows (now addressed)

- Queryable over HTTP exercised only `Equals`/`IsTrue`/search; no groups, multi-sort, typed values
  (date, decimal, Guid, DateTime, nullable), negated operators, rejection codes, async sources/views,
  view-specific paging, required view parameters, forwarding, or EF Core execution.
- Process: no functional `[AvailableUntil]`, `Reject()`, throwing handler, outbound or inbound
  handoff, 409 conflict, multi-step dependency ordering, `/execute` resume or idempotency, typed
  process client against a real server, step-input redaction, `PlanBuilt` event.
- Cross-cutting: aggregation tested only with a failing downstream; no 500 shape, no error-path
  correlation echo, no malformed `X-Kaleido-Process-Id`, default client `RoutePrefix` never used.
- MCP: query filters/sort/page never sent; no business messages, rejected steps or user-audience
  annotations; no decline path; MRTR not deliberately exercised.
- Weak assertions: `QueryableClientTests` (`TotalCount > 0`), `QueryableDiscoveryTests` (duplicated
  assertion, no field metadata), `EventOutboxTests` (no `PlanBuilt`).

## 4. Brittle or hidden behavior

- Reflection dispatch (`MakeGenericMethod`) in `QueryableService`, both engines and the endpoint
  mappers: protected by every endpoint test; the async source/view shapes had no coverage.
- The validator, resolver and applier each resolve field names case-insensitively, while
  `Expression.PropertyOrField` is case-sensitive; it works because the applier uses the metadata's
  canonical name. Now pinned by `AFieldName_IsMatchedCaseInsensitively_*`.
- `ToApiBody` serializes condition values with **default** `JsonSerializerOptions`, not
  `KaleidoJsonOptions`: an enum without a string converter is sent as a number, and works only
  because the downstream resolver parses the number text. Round trip pinned for the converter case.
- `StepCandidateBuilder` uses its own serializer options, separate from the endpoint's.
- `KaleidoStartupFilter` order (exception middleware outermost) is load-bearing for the
  `BadHttpRequestException` thrown inside the observability middleware: pinned by
  `AMalformedProcessIdHeader_*`.

## 5. Cross-component assumptions now protected

- Queryable wire contract: `QueryApiBody` -> resolver types -> validator -> compiler -> applier
  (`QueryContractTests`).
- `ToApiBody` (core -> wire) agrees with `QueryBodyResolver` (wire -> core) (`AForwardedQuery_*`).
- Response enum values are camelCase strings a filter accepts back (`EnumValues_*`).
- Kaleido's generated expressions translate to SQL through a projection (`EfCoreQueryableTests`).
- Registry metadata equals what the runtime enforces (`GetRegistry_PublishesEachFieldsDeclaredCapabilities`, `*ViewsOwnPaging*`).
- Typed client and server agree on every value type (`QuerySourceAsync_*ValueForValue`) and on errors.
- Process client uses the default route prefix (lower-cased client name) and resumes by id
  (`TheTypedClient_*`).
- A router's registry merges a live downstream (`RegistryAggregationTests`).
- MCP tool schema (fields/operators) is what the service accepts (`AQueryTool_UsesTheFieldsAndOperatorsItAdvertises_*`).

## 6. Tests added or changed

| Test | Protects against |
|---|---|
| `Queryable/Execution/QueryContractTests` (new, 19 cases in the operator theory) | wrong operator semantics, value conversion, groups, sort order/sequence, search, composition, rejection codes, 400-not-500 for bad values, async source/view, per-view paging, required parameters, forwarding round trip |
| `IntegrationTests/EfCore/EfCoreQueryableTests` (new) | expressions EF Core cannot translate; SQL semantics for nulls, enums, Guid, DateTime, In/Between; paging + count; view projection |
| `Processor/Behavior/ProcessBehaviorTests` (new) | DependsOn, AvailableUntil, AvailableAfter, Repeatable, non-repeatable rerun, Reject leaves state, DataAnnotations, unexpected failure hidden + retryable, handoff out/in, self-handoff refused, 409 conflict saves nothing, multi-step ordering, `/execute` resume and idempotency, error-path RequestId echo, 500 shape, malformed process id, typed process client |
| `Registry/RegistryAggregationTests` (new) | aggregated registry missing downstream capabilities; strict read of a healthy aggregate |
| `Mcp.FunctionalTests/McpContractTests` (new) | query arguments reshaped wrongly; service errors mapped to internal errors; business messages lost or not marked for the user; rejection changing state; MRTR round trip; decline submitting answers; questions lost for older clients |
| `QueryableDiscoveryTests` (strengthened) | registry dropping descriptions, classifications, operators, capabilities, view paging; removed a duplicated assertion |
| `QueryableClientTests` (strengthened) | client/server JSON drift for any value type; error mapping |
| `EventOutboxTests` (strengthened, +1) | `PlanBuilt` not published; classified step input published in the clear |

Model changes: `FunctionalRecordContext` gained operators and Guid/DateTime fields, an async source
and an async view with their own paging; `RuntimeInputStep` gained a classified `Remark`; the clinic
gained ward/age fields, paging, and a close handler that rejects or reports a business message. The
Behavior models live in their own namespace; only `BehaviorFixture` and `RegistryAggregationTests`
register them.

## 7. Intentionally not functional-tested

- Startup registration failures (duplicate names, missing handlers, cycles, missing attributes): a
  host cannot start with them; unit tests own them.
- Per-operator x per-type exhaustiveness and error-code strings: unit tests of the applier and the
  resolver; functional tests keep one case per class of behavior.
- Atomicity of state + event in one save: needs fault injection mid-transaction.
- Relay lease/timeout edge timing, cache TTL internals, header-sanitizer matrix, OTel instrument
  names, option validation, name truncation/collision hashing, catalog cache eviction: unit level.
- Search priority: fields are OR-ed, so priority is not observable in results.

## 8. Architectural risks and findings (not fixed here)

1. **MCP session elicitation is unreachable over the shipped HTTP transport.** The adapter uses
   stateless HTTP; a client on an initialize-based revision (e.g. `2025-11-25`) that advertises
   forms has no session, so the adapter falls back to returning the questions to the agent. The
   README promises "clients with a session get an elicitation request during the call". Behavior is
   safe (questions are never lost, pinned by `WithAnOlderRevision_TheQuestionsAreNeverLost`), but
   the documented path only exists for non-HTTP transports. Decide: document, or offer stateful HTTP.
2. **Documented endpoint does not exist:** `GET /{prefix}/processes/steps/{step}/metadata` is listed
   in `src/Kaleido.Http/README.md` and `src/ARCHITECTURE.md` but is not mapped.
3. **Count materializes rows:** the default `QueryContextExecutor.CountAsync` enumerates every
   matching row of an async (EF Core) query to count it, instead of `SELECT COUNT(*)`. Documented as
   an extension point, but the default is a performance trap for large sources.
4. **`ToApiBody` value serialization** uses default JSON options (see 4); a delegated source over an
   enum without a string converter depends on accidental numeric parsing downstream.
5. **Redundant guard:** `StepCandidateConsistencyChecker`'s "already completed" path is unreachable
   over HTTP because the availability resolver already removes completed non-repeatable steps.
   Defense in depth, not a defect; disabling it changes no behavior.
6. **`ToolCatalogProvider` cache is static**, shared by every host in a process (keyed by listing and
   revision). Harmless today; a hidden coupling between hosts.
7. **Still open (identified, not yet covered end to end):** ZeroTrust authorization mode on a real
   host; idempotency keys scoped per caller (two users, one key); `?refresh` on an aggregated
   registry; a transiently failing execute POST retried by the client's resilience pipeline with its
   `Idempotency-Key` (runs once); MCP guided lookups, handoff follow-up and `describe_tool`; the
   registry URL host guard (SSRF) with a real registry payload. Each is unit-tested in isolation.
