# Changelog

All notable changes to the Kaleido packages. The packages share one version
([`docs/VERSIONING.md`](docs/VERSIONING.md)); during the preview any release may contain breaking
changes, each named in its pull request. The format follows [Keep a Changelog](https://keepachangelog.com/);
from 1.0 the versions follow [Semantic Versioning](https://semver.org).

## [Unreleased] - 1.0.0 previews

The preview builds (`1.0.0-preview.{height}`) published to the repository's GitHub Packages feed.
The changes below are grouped by area; the pull requests carry the details and their breaking-change
sections.

### Breaking changes
- **Authorization is enforced by default:** `AuthorizationMode` defaults to `Authenticated`; a host
  without authentication sets `None` explicitly ([ADR 0021](docs/decisions/0021-authenticated-by-default.md), #400).
- **One credential model for outgoing calls:** `IKaleidoCallCredentialProvider`; an MCP host registers
  `ForwardCallerToken()` or its own provider ([ADR 0022](docs/decisions/0022-call-credentials.md), #403).
- **Store contracts split** into `IKaleidoContextStore`, `IKaleidoOutboxStore` and
  `IKaleidoIdempotencyStore` ([ADR 0020](docs/decisions/0020-three-store-contracts.md), #399).
- **Outbox integration** ([ADR 0023](docs/decisions/0023-outbox-integration.md), #408): the context store is
  state only (`SaveAsync(context)`); every event goes to `IKaleidoOutbox` - Kaleido's built-in outbox
  (default), the host's own (`AddOutbox<T>()`) or `AddDirectPublication()` - and the optional
  `IKaleidoTransactions` makes a state change and its event atomic. `IKaleidoOutboxStore.EnqueueEventsAsync`
  is now `AddAsync`. New builder methods `AddContextStore`, `AddIdempotencyStore`, `AddOutboxStore`,
  `AddTransactions`; new startup codes `conflicting_store`, `conflicting_outbox`.
- **Conditional questions** ([ADR 0024](docs/decisions/0024-conditional-questions.md), #235):
  `InformationItem.EnableWhen` (`InformationCondition`: question, operator, answer) and `EnableBehavior`.
  Only enabled questions must be answered; an answer to a disabled one is rejected. The MCP adapter asks
  in rounds (forms, and chat with `answersToken`) and never shows the agent the conditions. Additive on
  the wire.
- **Output descriptions for agents** (#213): new consumer rule KAL2024 (step results and view outputs
  need descriptions; an error in the samples). MCP query and step results carry `fieldDescriptions`.
- **SQLite registrations:** `UseSqliteKaleidoContextStore` is replaced by `AddSqliteContextStore`,
  `AddSqliteOutboxStore` and `AddSqliteIdempotencyStore` (one database; `connection_string_mismatch`);
  `ISqliteKaleidoTransaction` lets a host's own outbox join the SQLite transaction (#408).
- **Idempotent execution admission:** the idempotency store's `AdmitAsync`/`CompleteAsync`/`ReleaseAsync`;
  new 409 codes `pro_idempotency_in_progress` and `pro_idempotency_outcome_unknown`
  ([ADR 0019](docs/decisions/0019-idempotent-execution-admission.md), #397).
- **Repeated registration:** a second `AddKaleido()` or `AddHttp()` is a startup error (`duplicate_registration`, #400).
- **Kaleido's HTTP behavior only on Kaleido's endpoints:** no whole-host middleware or startup filter (#394).
- **Error responses:** 500 bodies and span status never carry exception text (#393); `/execute` answers
  400 for a step it cannot build (#392).
- **Public API surface:** core runtime internals and registry descriptors made internal; wire contracts
  self-contained in `Kaleido.Http.Abstractions` ([ADR 0017](docs/decisions/0017-transport-contract-placement.md), #389, #390).
- **Consumer analyzer rules:** KAL2008 removed; KAL0004/KAL1011 removed (#401).

### Added
- Front-door provenance on events and spans: `Origin`/`OriginTool` end to end, `CallerActor` per hop (#403).
- MCP trust-boundary, instructions-resource and integration documentation; functional trust tests (#403).
- Help links on every consumer analyzer rule; a clean-consumer package test in CI (#401).
- Release workflow publishing to GitHub Packages; `RELEASING.md`; this changelog (#318).

### Fixed
- Analyzer packaging and rules, cancellation finalization, SQLite provider, sample handoff authorization,
  client registry cache (#382).

## [1.0.0] - planned

1.0 is the contract freeze ([#241](https://github.com/no1ross/Kaleido/issues/241)): from 1.0 a breaking
change to the public API, the HTTP wire contracts, the error codes or the telemetry names needs a new
major version. Before 1.0.0 is tagged:
- the intentional public API and its compatibility baseline are recorded (#313);
- the remaining pre-1.0 work items of [#284](https://github.com/no1ross/Kaleido/issues/284) are
  resolved or deferred by decision;
- the publication target for stable packages (nuget.org, with symbols) is decided and set up;
- this section is replaced by the release's entry, listing the changes since the last preview.
