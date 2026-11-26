# 0044. Process.Server owns neutral idempotency admission and replay

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** #589, #587, #600, #602; clarifies the current ownership of the contracts described historically in ADR 0019 and ADR 0023

## Context

Generic Process plans and persists step transitions; it does not use the idempotency store. The earlier Process-owned store and replay record included HTTP-shaped status/response details even though a non-HTTP adapter must be able to admit and replay a call. After Process moves from `Kaleido.Processor.*` to folder-aligned `Kaleido.Process.*`, retaining these transport concerns in Process or the foundation would give them the wrong owner. Historical ADRs 0019 and 0023 record the original admission and outbox decisions and are not rewritten.

## Decision

1. Generic `Kaleido.Process` owns steps, runtime, `ProcessContext`, `IProcessContextStore` and `IProcessTransactions` under `Kaleido.Process`/`Kaleido.Process.Context`/`Kaleido.Process.Storage`, and `ProcessConcurrencyException` under `Kaleido.Process.Exceptions`. Keep `Processor*` for the hosting runtime/registry vocabulary, not as a compatibility namespace. `AddProcess()` discovers/registers the runtime but does not choose an in-memory context or idempotency store. The later Provider's `AddInMemoryProviders()` is an explicit development choice.
2. The distinct `Kaleido.Process.Server` project owns `Kaleido.Process.Server.Idempotency`: `IProcessIdempotencyStore`, `IdempotencyRecord`, `IdempotencyState`, `IdempotencyReplay`, `InMemoryProcessIdempotencyStore` and explicit `AddIdempotencyStore<T>()`. This is a server-side **transport-neutral** admission contract, not an endpoint or a Core store. A completed record contains a replay with a required format identifier and opaque payload bytes. In-progress and unknown records have no replay. The transport scopes a key, hashes its request, chooses the replay format/encoding and maps outcomes to its own response. Atomic admission, admission-ID fencing of late completions/releases after expiry and re-admission, lease and unknown-outcome retention remain contract invariants.
3. Process transitions begin, save state, add the Eventing outbox record and commit or roll back. Atomicity needs both stores to join the **same provider session/transaction**, not merely share a database; this is provider-neutral and not a SQLite- or Core-owned transaction abstraction.
4. Every service needs a valid `ServiceVersion` from code or configuration (ADR 0043), not only HTTP services. `ProcessKey.ProcessorVersion` and persisted `ProcessContext.ProcessorVersion` retain nullable shapes for legacy reads. Do not rewrite persisted records or change stable event discriminators, error-code strings or telemetry names as a side effect of moving CLR types.

## Alternatives considered

- Leave the idempotency API in generic Process or Core. Rejected: neither runtime nor foundation admits a server request, and HTTP-shaped replay would keep transport behavior in the wrong owner.
- Encode status code/JSON in the neutral replay, or prescribe one universal replay format. Rejected: response representation belongs to each transport; opaque bytes plus a format identifier allow adapters to evolve independently.
- Move Process transactions to Core, or assume one database guarantees atomicity. Rejected: the writer and state store must join one provider session; Core cannot promise provider-specific participation.
- Automatically install memory stores from `AddProcess()`. Rejected: it hides the provider choice and would conflate generic runtime and server admission.

## Consequences and deferrals

This is a preview namespace/type/API break for Process consumers and providers. HTTP, SQLite, Provider, Hosting and Platform are **not migrated by this decision's generic-project unit**. #587 remains open for the HTTP filter, SQLite/provider implementations, replay encoding and schema/old-row compatibility; no existing HTTP/SQLite path is claimed to work with the neutral contract yet. Do not silently convert old replay rows or promise replay compatibility. Migrate each higher owner with its own tests and explicit compatibility decisions. #600 covers Process options behavior; #602 tracks the relevant follow-up. Process state/event wire and persisted property names, error-code values and metric strings are not renamed; the old idempotency reply fields are deliberately replaced and need the separate #587 persistence/adapter migration.
