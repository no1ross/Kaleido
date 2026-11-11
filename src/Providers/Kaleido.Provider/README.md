# Kaleido.Provider

This temporary base project offers explicit `AddInMemoryProviders()` registration for Process state, idempotency and Eventing outbox stores when they have no other implementation. Process and Eventing still contain those concrete implementations during this migration; their constructors warn when in-memory persistence is used. Neither `AddProcess()` nor `AddEventing()` silently selects an in-memory store.

In-memory data is lost on restart and is not shared between instances. Production hosts register durable stores instead. `Kaleido.Hosting` checks that required store contracts have *some* registration at startup; it does not reject an explicitly registered in-memory store. The final migration of in-memory implementations to `Kaleido.Provider.InMemory` remains in #559. See [dependency boundaries](../../../docs/architecture/dependencies.md) and the [persistence guide](../../../docs/guide/persistence.md).
