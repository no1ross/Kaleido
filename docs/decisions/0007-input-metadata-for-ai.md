# 0007. Input metadata is a published hint, nudged by analyzer warnings; verbatim text; AI safeguards live in the adapter

- **Status:** Accepted
- **Date:** 2026-10-08
- **Related:** #142, #140, #137, #139

## Context
Once AI agents consume the registry, display text *is* the interface: an agent can only ask for
what the metadata describes. Kaleido can't dictate how a UI or an IVR phrases a question, and
it can't tell a person from a process.

## Decision
- **Property-based steps:** `[Display(Name, Prompt, Description)]` and `[Description]` are
  published as field metadata (`displayName`, `prompt`, `description`). They are **hints**: the
  client may phrase the question its own way. There is no per-client text provider and no
  projection into questionnaires.
- **Analyzers nudge without being strict:**
  - KAL2017 (a step input without a description) and KAL2018 (an `IQueryContext` or
    `IQueryParameters` property without a description) are **warnings**, and only a
    description is asked for;
  - KAL2015 and KAL2016 are **errors**, because they mirror runtime failures.
- **Verbatim:** business messages and information-request text are always delivered verbatim.
  `[Display]` hints may be phrased naturally. Pronunciation belongs to the channel.
- **No "a human must answer" rule in core:** Kaleido only sees an authenticated identity.
  Keeping an AI intermediary from inventing answers or paraphrasing verbatim text is the MCP
  adapter's job (#140: elicitation plus explicit instructions; see #137 and #139).

## Alternatives considered
- **Required metadata as errors:** too strict for property hints, and blocks adoption. The
  owner runs warnings-as-errors where wanted.
- **A per-client text provider in core:** that is client wording, i.e. configuration, and
  exact client wording belongs in information requests and business messages.
- **A per-item "verbatim" flag:** easy to forget. The rule is by kind instead.

## Consequences
- The registry carries enough description for agents to choose steps and ask for inputs.
- Samples will gain descriptions as they adopt the consumer analyzers (#206, #209).
