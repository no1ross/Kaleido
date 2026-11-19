# 0049. One explicit provider per store; in-memory implementations live in their provider

- **Status:** Accepted; supersedes the ordering and temporary placement in [ADR 0048](0048-explicit-provider-registration-without-default-stores.md)
- **Date:** 2026-11-18
- **Related:** #585, #559, #598, #623; ADR 0028, ADR 0044, ADR 0048

## Context

ADR 0048 removed implicit memory defaults but temporarily registered the Process, Process.Server and Eventing in-memory implementations from `Kaleido.Provider`. Its ability to silently replace a prior memory choice with a later durable choice required those lower assemblies to know their own concrete memory types. Moving the implementations to the requested `Kaleido.Provider.InMemory` package would make that type dependency point upward. Provider choice has no registration sequence: a host explicitly chooses the provider for each store it uses, and a different second choice is an error rather than a priority rule.

## Decision

1. Existing store interfaces stay with their owners: `IProcessContextStore`/`IProcessTransactions` in Process, neutral `IProcessIdempotencyStore` in Process.Server, `IKaleidoOutboxStore` in Eventing and the authority's `IKaleidoRegistryStore` in Registry.Server. The base `Kaleido.Provider` project is their dependency bundle, not another contract hierarchy, a hidden default or a no-op registration. Concrete technology packages depend on it and on any extra interface owner they implement (SQLite also implements Registry.Server's store in the **same SQLite project**).
2. `Kaleido.Provider.InMemory` owns the concrete Process state, idempotency and outbox memory stores. `AddInMemoryProviders()` explicitly chooses all three; each `AddInMemory*Provider()` chooses just its named store. Each store is a singleton per host, and each warns on construction that its records are lost on restart and not shared among instances. A separately hosted control Registry chooses its own authority store explicitly.
3. For each store contract, registering the same provider again is idempotent. Registering a **different** provider is a `KaleidoConfigurationException` (`conflicting_store`, or `conflicting_outbox`) in either order. The aggregate memory choice checks for conflicts before registering any of its stores. The lower `Add*Store<T>()` APIs no longer recognize a higher-package memory type as a replaceable default; the Process no-op transaction fallback remains replaceable by a real transaction provider. This preserves one-way references without a store-choice marker, reflection on an upper assembly or an environment-dependent selection rule.
4. Hosting validates only that required stores are registered and accepts an explicit memory choice. Process state and Eventing outbox are atomic only when the chosen durable stores join **one provider session**; memory is not a durability or atomicity substitute. No release of the full #585 package set is claimed before its final clean-consumer gate.

## Alternatives considered

- Give a later durable registration priority over earlier memory: the same two choices have different behavior depending on order, and Process/Eventing would have to identify a type owned by a higher provider package or invent a marker.
- Last registration wins for every provider: two conflicting durable configurations could silently select the wrong database.
- Leave memory implementations in Process/Process.Server/Eventing permanently: the provider package would only wrap lower-level concrete implementation details instead of owning its store behavior and tests.
- Move the Registry's in-memory authority store here or split SQLite into a Registry-specific provider: neither is required for the three-store memory choice or the single SQLite technology implementation.

## Consequences

This is a pre-1.0 registration and package move from the temporary #598 arrangement. A host updates its `AddInMemoryProviders()` import/reference to `Kaleido.Provider.InMemory`; a host using one in-memory concern can select it individually. A host naming both memory and a durable provider for the same store must choose one; changing method order is not a fix. Error-code values, persisted Process/Eventing event shapes, warning levels, idempotency retention and SQLite schema do not change. ADR 0048's no-implicit-default and missing-store-presence principles remain; its temporary base-project registration and durable-over-memory ordering do not.
