# 0028. Every store a service uses is chosen explicitly; memory is a choice, never a fallback

- **Status:** Accepted
- **Date:** 2026-10-25
- **Related:** #438, #430, ADR 0020, ADR 0023

## Context
`AddKaleido()` registers in-memory defaults for the process-state store, the idempotency store and
the outbox store (ADR 0020, ADR 0023). Each logs a Warning when it is created, but a host that never
registers a durable store still starts, and in production it loses every process, idempotency record
and pending event on restart, and shares none of them between replicas. The pre-1.0 review (#430)
found that a warning-only default is unsafe for a production-capable 1.0. The explicit provider
choices themselves (SQLite reference, custom stores, the host's own outbox, direct publication) are
sound architecture and stay.

## Decision
1. **The defaults stay registered, but they are not a choice.** When the host starts, Kaleido checks
   the final registrations: every store the service *uses* must have been chosen. If one is still the
   in-memory default, the host does not start (`KaleidoConfigurationException`,
   `stores_not_chosen`), and the message names the store and the ways to choose.
2. **Memory is chosen with `AddInMemoryStores()`.** It covers whatever stores remain in memory, for
   development, tests and demos. Durable stores registered alongside are kept. The in-memory stores
   still log their Warning.
3. **Only the stores in use are checked.**
   - The context and idempotency stores, when the service has process steps. A query-only service
     uses neither.
   - The outbox store, when events are written through Kaleido's built-in outbox (an event publisher
     is registered). The host's own outbox (`AddOutbox<T>()`) and direct publication
     (`AddDirectPublication()`) use no outbox store, and a service without a publisher writes no
     events.
4. **Startup validation, not environment inference.** The check runs for every environment: a
   production host that forgot is refused like any other. It reads the registrations as the host
   left them, so the order of the `Add*` calls does not matter.

## Alternatives considered
- **Keep the defaults with a warning.** Documented, but a forgotten store still loses data in
  production; the warning is easy to miss in a busy log.
- **Allow the defaults in Development only.** Infers safety from an environment name, which a host
  can misconfigure, and makes tests and production behave differently.
- **Remove the defaults.** Every host would have to register three stores before its first run,
  including the ones it does not use.
- **Fail at registration.** The durable stores are usually registered after `AddKaleido()`, so only
  the started host knows the final choice.

## Consequences
- Breaking (preview): a host with process steps, or with an event publisher on Kaleido's outbox,
  that relied on the in-memory defaults must call `AddInMemoryStores()` or register durable stores.
  The guide, the READMEs and the repository's test hosts now do one or the other.
- A container built without starting the host (`BuildServiceProvider()` in a unit test) is not
  checked; only a started host is.
