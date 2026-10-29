# 0025. Events have their own redaction policy, separate from logs

- **Status:** Accepted
- **Date:** 2026-10-21
- **Related:** #278, #139, ADR 0013 (amends its redaction policy)

## Context
ADR 0013 redacts classified values (`DataClassificationAttribute`, e.g. `[Phi]`, `[Pii]`) in event
payloads before the outbox write. It used the host's `IRedactorProvider`, registered with
`AddRedaction` - the same provider .NET's log redaction uses. One container has one provider, so logs
and events always shared one policy.

A common requirement is a different policy per channel: **logs never carry PHI or PII; events keep
PII (the downstream systems need contact details) but not PHI.** With one shared policy that was
impossible.

## Decision
1. **Events have their own policy:** `AddEventRedaction(r => ...)` on the Kaleido builder,
   with the standard `IRedactionBuilder` API (`SetRedactor<T>(sets)`, `SetFallbackRedactor<T>()`), so
   Microsoft's extensions (`SetHmacRedactor`, ...) and any `Redactor` work in it. It never registers an
   `IRedactorProvider`: the host's `AddRedaction` remains the log policy, untouched.
2. **Matching:** a classified value gets the redactor of its exact set of classifications, else the
   fallback, else it is erased. `NullRedactor` keeps a classification unredacted in events.
3. **Safe default:** without `AddEventRedaction`, every classified value in events is
   erased and a warning says so once - also when the host has a log policy that would keep it.
4. **The same attributes serve both channels;** only the configuration differs. Code-based event
   redactors (`AddEventRedactor<T>()`, `IKaleidoEventRedactor`) are unchanged and still run first, for
   what attributes cannot express (information-request answers, per-view rules).
5. **Naming:** `AddEventRedaction` - not `AddEventRedaction` - so it does not read like
   `AddEventRedactor<T>()`, which registers code-based redactors.

```csharp
builder.Services.AddRedaction(r =>                                   // logs: no PHI, no PII
    r.SetRedactor<ErasingRedactor>(new DataClassificationSet(Phi), new DataClassificationSet(Pii)));

builder.Services.AddKaleido(builder.Configuration)
    .AddEventPublisher<WarehousePublisher>()
    .AddEventRedaction(r =>                            // events: PII kept, PHI erased
        r.SetRedactor<NullRedactor>(new DataClassificationSet(Pii)));
```

## Alternatives considered
- **Keep the shared provider, add per-channel overrides inside it.** One `IRedactorProvider` cannot
  know which channel asks; a wrapper would have to guess. Rejected.
- **A Kaleido-specific mapping API** (classification to redactor type). It would duplicate
  `IRedactionBuilder` and lose Microsoft's extensions. Rejected.
- **Per-destination policies** (several publishers, one outbox entry per destination). Not needed yet;
  possible later on its own merits (#278, "later").

## Consequences
- **Behavior change (preview).** A host that relied on `AddRedaction` for events now gets erasure, with
  the warning, until it calls `AddEventRedaction`. Erasure is the safe direction: nothing
  is published that was not before.
- **Redactor types are singletons**, registered by the builder as `AddRedaction` does; options a
  redactor reads (e.g. HMAC keys) are shared with the log policy when both use the same redactor type.
- **Proven in one host:** `tests/Kaleido.IntegrationTests/Redaction/EventAndLogRedactionTests.cs`
  (logs erase PII while events keep it; events erased without an event policy).
- The PriorAuth sample sets "events: PHI erased, PII kept" for every service that publishes events.

## Erratum (#457)
Point 5 reads "`AddEventRedaction` - not `AddEventRedaction`" because a later rename (14725b52) was
applied to it mechanically. At the decision the method was `AddEventClassificationRedaction`; it was
renamed **`AddEventRedaction`**, which is the method's name. The decision of point 5 stands in that
form: the method that sets the event policy is `AddEventRedaction`, distinct from
`AddEventRedactor<T>()`, which registers code-based redactors.
