# Kaleido contributor guide

This is the repository-level contributor guide for Kaleido: 12 shipped projects and four gRPC placeholders (see [Project boundaries](#project-boundaries)). Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) first for the repository model, then the README of the project you are changing. The coding conventions are in [`docs/CONVENTIONS.md`](./docs/CONVENTIONS.md); this file stays under 16,000 bytes so agent tooling loads it whole (#455, checked in CI).

## Build and test commands

### SDK and target framework
- **Building Kaleido needs the .NET 10 SDK** (`global.json`: 10.0.100, `rollForward: latestFeature`; C# 14). `global.json` searches the running `dotnet`'s own SDKs first, then `C:\dotnet`, where an SDK installed from a zip lives on a machine that allows no installer (#578). Moving to a newer SDK is a deliberate change with its own Issue. That's a requirement for framework contributors only.
- **The published packages target `net8.0`** (`Directory.Build.props`), so any consumer on .NET 8 or later can use them. Don't raise `TargetFramework` because the SDK is newer; changing it is an explicit owner decision.

### Build and run tests
```
dotnet build Kaleido.slnx
./tools/test/Run-Tests.ps1                              # Debug; -Configuration Release to match CI
```

Test projects use xUnit v3 on **Microsoft Testing Platform (MTP) v2**: each test project is an executable, and `global.json` runs `dotnet test` on MTP (`dotnet test --solution Kaleido.slnx`). The gate is the script: it runs every built test project and prints one summary, with the checks below (CI and the release workflow run the same script). A project that fails prints its full output; one that runs no test fails too. To run a single project, run its assembly: `dotnet tests/<Project>/bin/Debug/net8.0/<Project>.dll` (MTP options such as `--filter-class` go after it). A project that fails to run or reports zero tests: see [stale build output](./docs/CONVENTIONS.md#when-a-test-project-fails-to-run-or-reports-zero-tests).

### Per-commit quality gate

Every commit must pass this gate on its own. A later commit fixing an earlier one doesn't count.

1. **Fresh build:** `dotnet build Kaleido.slnx --no-incremental` → **0 errors, 0 warnings**.
2. **All tests pass:** `./tools/test/Run-Tests.ps1` → `Passed!`, 0 failed, with a non-zero test count. The script itself refuses a test project missing from `Kaleido.slnx`, a stale or missing build output (it runs each project's evaluated `TargetPath`), a run without a summary and a project that ran no tests (#437); `tools/test/Test-RunTests.ps1` tests those rules (CI runs it).
3. **Record the result in the commit message body**, e.g. `Gate: build 0 errors / 0 warnings; tests 2981 passed`.

**Warnings fail the build in every area (#406)** - `src/`, `tools/` and `tests/`, IDE code-style rules included - so the "0 warnings" gate is enforced, not only observed.

CI (`build.yml`) also checks, and you run before committing:
- whitespace and final newlines: `dotnet format whitespace --folder --include src/ tools/ tests/ docs/ --exclude '**/obj/**' '**/bin/**' --verify-no-changes` (drop `--verify-no-changes` to fix) (#462);
- every Kaleido type or member named in the Markdown docs exists in the sources: `python3 tools/docs/check-doc-symbols.py` (ADRs are skipped as historical; a deliberate non-symbol in code style goes in the script's `ALLOWED` list with its reason);
- every tracked local Markdown link, image and heading fragment resolves: `python3 tools/docs/check-local-links.py`; test the checker with `python3 tools/docs/test-check-local-links.py` (external URLs are never fetched; use `python` on Windows);
- every public error code is raised in `src/` and listed in `docs/ERROR_CODES.md` (no more, no less): `python3 tools/docs/check-error-codes.py` (#518);
- every `AGENTS.md` is under 16,000 bytes: `python3 tools/docs/check-agents-size.py` (#455);
- the packed release set works in a clean host: after a Release build, pack to a fresh subfolder of `artifacts/` and run `./tools/packaging/Test-PackageConsumer.ps1 -Packages <folder>` and `./tools/packaging/Test-PackageConsumer.Tests.ps1 -Packages <folder>` (do not mix older packages in the feed; CI's `-RequireDeterministic` needs PowerShell 7);
- line coverage of the shipped `src/` assemblies, at least 95% ([build details](./docs/CONVENTIONS.md#build-details)).

## Project boundaries

One place owns each fact: a project's README lists its types and entry points, [`ARCHITECTURE.md`](./ARCHITECTURE.md) describes how the projects depend on each other and how a host registers them, and [`src/ARCHITECTURE.md`](./src/ARCHITECTURE.md) describes the internals. This section states only what each project owns and must not own; link to those documents rather than repeating their type lists.

| Project | Owns | Does not own |
|---|---|---|
| [`Kaleido`](./src/Kaleido/README.md) | Process and Queryable runtimes, eventing, store contracts, in-memory defaults | transport, ASP.NET Core, persistence technology |
| [`Kaleido.Platform`](./src/Platform/README.md) | shared registry/wire contracts, JSON, headers and metadata; changes ripple into server and client | endpoint mapping, runtime planning |
| [`Kaleido.Server`](./src/Server/README.md) | ASP.NET Core caller, authorization, correlation, filters, in-host registry view | protocol endpoints, runtime logic |
| [`Kaleido.Client`](./src/Client/README.md) | neutral clients, call context, registry participation, hydration, transport choice | HTTP wire implementation |
| [`Kaleido.Http`](./src/Http/Http/README.md) | shared HTTP route and URL shapes | server endpoints or outbound calls |
| [`Kaleido.Http.Server`](./src/Http/Http.Server/README.md) | HTTP Process and Queryable endpoints and execution adapters | core process decisions |
| [`Kaleido.Http.Client`](./src/Http/Http.Client/README.md) | one gateway HTTP client (`Kaleido:BaseUrl`), published-route calls, credentials and resilience | per-service addresses, server mapping |
| [`Kaleido.Mcp`](./src/Mcp/Mcp/README.md) | caller-filtered MCP tools, forms, schemas and invocation | transport or client internals |
| [`Kaleido.Registry`](./src/Registry/Registry/README.md) | control registry, leases, store contract, merged views and HybridCache | instance addresses and gateway routing |
| [`Kaleido.Registry.Http`](./src/Registry/Registry.Http/README.md) | control-registry HTTP API | registry state or runtime rules |
| [`Kaleido.Observability.OpenTelemetry`](./src/Observability/Observability.OpenTelemetry/README.md) | OpenTelemetry wiring | instrumentation emitted by core |
| [`Kaleido.Provider.SQLite`](./src/Providers/Provider.SQLite/README.md) | reference core and registry stores and transactions on SQLite | production-provider guarantees |
| [gRPC placeholders](./src/Grpc/Grpc/README.md) (`Kaleido.Grpc*`, `Kaleido.Registry.Grpc`) | future protocol boundaries, not packable yet | a gRPC implementation |

## General contributor rules

- **Kaleido is a process engine; it never makes business decisions** ([ADR 0001](./docs/decisions/0001-process-engine-not-decision-maker.md)). It applies declared rules (the step graph, declared roles/policies, structural validation) and records what happened; what to ask, who may access a case, which path to take and what wording to use belong to the implementer. New capabilities are mechanisms, never content.
- Keep concerns in the correct project.
- `Kaleido` (core) should remain free of transport-specific behavior.
- `Kaleido.Http.Server` should adapt and publish; it should not reimplement runtime logic.
- `Kaleido.Platform` is shared — changes here ripple into both server and client projects.
- Keep transport layers thin.
- Prefer explicit registration and discoverability over hidden behavior.
- Match documentation to the actual code and runtime behavior.
- When you change contracts or metadata semantics, review the downstream docs and tests for the affected area.
- Define response semantics before choosing a serialization format: keep distinct message streams separate and represent an empty collection consistently across transports. Options control which messages adapters populate, not JSON-specific field presence.

## Coding conventions

In [`docs/CONVENTIONS.md`](./docs/CONVENTIONS.md); read the section before changing code it covers:
- [Public API documentation](./docs/CONVENTIONS.md#public-api-documentation) - strong XML docs on every public member; every API change in `PublicAPI.Unshipped.txt` and ADR 0032.
- [Primary constructors](./docs/CONVENTIONS.md#primary-constructors) and [nullable suppression](./docs/CONVENTIONS.md#nullable-suppression-operators) - when to convert; no `!`.
- [Log levels](./docs/CONVENTIONS.md#log-levels) - Information is boundary signals only; caller errors are never Error.
- [Exception handling](./docs/CONVENTIONS.md#exception-handling) - Kaleido exceptions only; error codes owned by their area.
- [OperationCanceledException and observability](./docs/CONVENTIONS.md#operationcanceledexception-and-observability) - one signal per cancellation.
- [Time](./docs/CONVENTIONS.md#time) - `TimeProvider` where behavior depends on time, not for tests (KAL0021).
- [Records](./docs/CONVENTIONS.md#record-conversion), [collection expressions](./docs/CONVENTIONS.md#collection-expressions), [using statements](./docs/CONVENTIONS.md#using-statements).

## For code generators

- Read [`docs/PATTERNS.md`](./docs/PATTERNS.md) first — it is the canonical
  convention doc (step/handler shape, sync-vs-async sources, options
  snapshots, exceptions, DI, correlation).
- The vocabulary is: **Processor** = owns/executes steps; **Process** = one
  executing instance. See `src/ARCHITECTURE.md` → Terminology.
- `docs/ANALYZERS.md` has one section per audience, each listing its rules
  and what they prevent:
  - consumer `KAL2xxx` rules, for code that uses Kaleido (services, samples);
  - source `KAL0xxx` rules, for framework code in `src/`;
  - test `KAL1xxx` rules, for `tests/`.

  Generate compliant code; never suppress KAL rules.
- Correlation invariants (one RequestId per request, echo-or-generate,
  automatic outbound propagation) are in `src/ARCHITECTURE.md`.

## Tracking work discovered mid-item

GitHub Issues in `no1ross/Kaleido` are the system of record for active and historical work. At the start of a session, read the relevant open Issue and its linked Issues/PRs; do not use old review documents or private plans as status trackers. Every new change needs an Issue and a corresponding PR. Include `Fixes #N` in new commit messages and PR bodies for the Issue(s) the change resolves.

**Write done criteria in the Issue before starting work.** Anything outside those criteria is a discovered item.

**PR size: few, large PRs.** Each PR has a full CI, Sonar and CodeQL cycle and a review, so many small PRs cost more time than they save. Group a work package (for example, a whole analyzer rule family across all projects, or a feature with its docs and tests) into one PR:
- One PR per work package, not one per project, file or rule. One PR may resolve several Issues (`Fixes #A`, `Fixes #B`).
- No stacked PRs that must merge in a set order; branch from `master`.
- Keep a large PR reviewable: separate commits per step, and a PR description with one section per topic (what changed and why).
- Verification does not shrink with the PR count: build with 0 warnings and all tests before every commit, Sonar and CodeQL green on the PR.

When something new surfaces while working an Issue:
- Create a separate Issue **before** discussing a fix, and link it to the discovery Issue. Record its context, done criteria and relation rather than silently widening the current PR.
- Classify the relation; never dismiss it as "unrelated":
  - `requires`: add a GitHub sub-issue; the parent cannot close until it is resolved
  - `should`: link a related Issue to consider alongside the parent, but do not block it
  - `separate`: link a related Issue for independent scheduling
- **Scope gate:** ask "do the current Issue's done criteria require this?"
  - Yes → `requires`; do it now.
  - No → log it (`should` / `separate`) and **default to deferring** it to a future PR. Pulling it in requires an explicit decision.
- Propose the minimal fix that meets the done criteria first; offer broader redesigns as separate Issues.
- Timebox discovery: if a side thread grows after a couple of exchanges, stop and record what is known in the linked Issue.
- Before completing a PR, list open related and sub-issues in its summary; do not close a parent while a required sub-issue remains open.

## Documentation rules

- Root docs should explain how the projects fit together.
- Project READMEs should explain what lives in that project specifically.
- Contributor guides should focus on invariants, boundaries, and what not to change casually.
- Framework-shaping decisions (contracts, boundaries, principles) get a decision record in [`docs/decisions/`](./docs/decisions/README.md): numbered, never rewritten, superseded by a new record when they change, and linked to their issues/PRs.

## How to navigate the repo

- **Repository:** [`README.md`](./README.md), [`ARCHITECTURE.md`](./ARCHITECTURE.md), [`CONTRIBUTING.md`](./CONTRIBUTING.md), [`MAINTAINERS.md`](./MAINTAINERS.md), [`SECURITY.md`](./SECURITY.md), [`SUPPORT.md`](./SUPPORT.md), [`docs/VERSIONING.md`](./docs/VERSIONING.md), [`RELEASING.md`](./RELEASING.md), [`CHANGELOG.md`](./CHANGELOG.md) — the human-facing summary of these rules, who maintains the project, the vulnerability-reporting route, where to ask, and the preview policy.
- **Source:** [`src/AGENTS.md`](./src/AGENTS.md), [`src/ARCHITECTURE.md`](./src/ARCHITECTURE.md), [`docs/CONVENTIONS.md`](./docs/CONVENTIONS.md); project READMEs: [`Kaleido`](./src/Kaleido/README.md), [`Platform`](./src/Platform/README.md), [`Server`](./src/Server/README.md), [`Client`](./src/Client/README.md), [`Http`](./src/Http/Http/README.md), [`Http.Server`](./src/Http/Http.Server/README.md), [`Http.Client`](./src/Http/Http.Client/README.md), [`Mcp`](./src/Mcp/Mcp/README.md), [`Registry`](./src/Registry/Registry/README.md), [`Registry.Http`](./src/Registry/Registry.Http/README.md), [`OpenTelemetry`](./src/Observability/Observability.OpenTelemetry/README.md), [`SQLite`](./src/Providers/Provider.SQLite/README.md).
- **Tooling:** [`tools/analyzers/README.md`](./tools/analyzers/README.md) — analyzer project roles and test locations; rule catalog in [`docs/ANALYZERS.md`](./docs/ANALYZERS.md).
- **Tests and examples:** [`tests/AGENTS.md`](./tests/AGENTS.md); the consumer guide's compiled examples, [`docs/guide`](./docs/guide/README.md); the full samples in their own repositories, consuming the Kaleido packages: [`Kaleido-Samples-PriorAuth`](https://github.com/no1ross/Kaleido-Samples-PriorAuth) and [`Kaleido-Samples-ECommerce`](https://github.com/no1ross/Kaleido-Samples-ECommerce) (#95). Use the examples and samples to understand intended consumer usage, and the tests to understand behavioral expectations and invariants.
