# 0003. The framework owns the schema, the implementer owns the content; two kinds of steps

- **Status:** Accepted
- **Date:** 2026-10-08
- **Related:** #142, #209

## Context
Questions come from rules engines, clinical algorithms, form builders and client
configuration, and they vary endlessly. Some step inputs are known up front, others only at
runtime. Mixing the two on one step puts the same data in two places.

## Decision
- **Kaleido provides the mechanism only:** it carries a request, records that the process is
  waiting, and checks that the answers fit. Generating the questions is always the
  implementer's job (following [0001](./0001-process-engine-not-decision-maker.md)).
- **Two kinds of steps:**
  - a step with properties, for known inputs;
  - an information step (`IInformationStep`), whose payload *is* the answers. It declares only
    `InformationRequestId` and `Items`, enforced at startup (`pro_invalid_registration`) and by
    KAL2015.
- **The interface is the marker**, consistent with `IProcessStep` as step identity (#195).
  Handlers are ordinary `IProcessStepHandler<TStep>`; there is no special handler type.
- **`[Repeatable]` stays orthogonal:** an information step without it is one questionnaire and
  one response; with it, the step loops by requiring itself with the next request.
  `[Repeatable]` means only "may run again"; replace or accumulate is the handler's logic.

## Alternatives considered
- **Framework adapters for question sources** (Form.io and others): sources vary per vendor and
  client, so Kaleido would become a forms engine.
- **A base record for information steps:** simpler for implementers, but less consistent with
  interface-as-identity. The interface was chosen; each step declares its two properties.
- **A sibling `informationResponse` envelope field on every request:** an empty slot on most
  steps, and two places for data.
- **Projecting property-based steps into questionnaires:** generated wording isn't
  authoritative, and the same information would be described twice.

## Consequences
- Implementers own question builders. The PriorAuth sample's builder is interim; #209 makes it
  generic and configuration-driven.
- Information steps publish no fields in the registry; the pending request describes them.
