# 0048. Providers are explicit; memory is never a default

- **Status:** Accepted; supersedes the default-store/marker mechanism of [ADR 0028](0028-stores-are-chosen-explicitly.md); temporary Provider placement and ordering superseded by [ADR 0049](0049-one-provider-per-store.md)
- **Date:** 2026-11-18
- **Related:** #585, #598, #597, #559, #587; ADR 0020, ADR 0023, ADR 0044

## Context

ADR 0028 kept in-memory stores registered implicitly and made Hosting distinguish an unchosen default from a chosen store. After the #585 capability split, generic Process owns state and transactions, Process.Server owns separate neutral idempotency admission, and Eventing owns its outbox. Foundation cannot choose a store for them or inspect provider technology. An in-memory instance must survive multiple request scopes within its host, but cannot protect state, an outbox or idempotency replay across host restarts. A default store, even with a startup warning, can silently lose production data.

## Decision

1. `AddKaleido()`, `AddProcess()` and `AddEventing()` register no store implementation by default. A host explicitly chooses durable implementations with `AddContextStore<T>()`, `AddIdempotencyStore<T>()` and `AddOutboxStore<T>()`, or deliberately calls `Kaleido.Provider.AddInMemoryProviders()` for development and tests. No store-selection marker or contributor belongs in foundation.
2. The temporary Provider assembly `TryAdd`s the existing Process context, Process.Server idempotency and Eventing outbox in-memory implementations as **singletons**: their records and admission fences remain visible across request scopes within one host. It neither creates another store implementation nor changes the implementations' warnings. A previously chosen durable store is retained; a later explicit durable registration replaces the in-memory reference with its scoped store. Calling the memory choice twice is idempotent.
3. Hosting checks only the presence of the stores the registered work needs, irrespective of provider technology, and keeps the `stores_not_chosen` diagnostic for missing registrations. Generic Process does not require server-side idempotency admission in a console host; the HTTP Process server requires its neutral idempotency store when mapped. Memory is explicitly allowed but loses everything on restart and does not provide an atomic Process state/Eventing outbox session. Production stores sharing a transaction must participate in the **same provider session**, not merely a database.
4. The concrete in-memory stores remain inside Process, Process.Server and Eventing until the separate `Kaleido.Provider.InMemory` move (#559). Provider and composed hosts reference the lower owners, never the reverse. SQLite schema, durability and replay-row migration belong to their own #587/#558 work; no old-row compatibility is inferred here.

## Alternatives considered

- Keep implicit memory plus a marker distinguishing selected from unselected defaults: a host may still run with an accidentally lossy implementation, and the marker creates an unnecessary foundation/provider coupling.
- Choose memory inside `AddProcess()` or `AddEventing()` or infer it from a Development environment: this hides the provider decision and changes behavior with an environment label.
- Register memory as scoped: a new request scope would lose the earlier process state, admission record or outbox events.
- Move the implementations into the final InMemory package now: #559 owns that separate project and its full consumer/pack migration.

## Consequences

This is a pre-1.0 startup/registration change: a host that used an implicit in-memory default must choose `AddInMemoryProviders()` or durable stores. The temporary base Provider remains non-packable until its final owner work is complete. Hosting's missing-provider check is about presence, never an in-memory implementation-type ban. No SQLite replay-row conversion or compatibility promise is made in this project unit; #587 owns that decision. ADR 0028 remains historical for the explicit-choice principle, but its default-store and `AddInMemoryStores()` mechanics are superseded.
