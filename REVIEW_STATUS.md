# Kaleido Pre-Release Review: Status

**Created:** September 25, 2026  
**Updated:** September 26, 2026 (v2.1 external-review merge)  
**Status:** v2.1 COMPLETE — ready for team design review

---

## What Changed in v2.1

Two external reviews (`cleanup chatgpt ent.md`, `cleanup copilot.md`) were merged in. Both were produced from a compressed Repomix export, so every claim was re-validated against source before acceptance. Most external items deduplicated into existing findings; 19 new EXT findings were added (EXT-01…EXT-19).

Two high-impact external claims were verified directly in source:

- **EXT-03 (VERIFIED):** `QueryContextExecutor` runs synchronous LINQ (`Count()`, `ToList()`) behind async query APIs — thread-pool blocking on the hot path.
- **EXT-05 (VERIFIED):** `Kaleido.Analyzers` is a packable shipping package containing zero rules.

Headline new additions: **EXT-01** (no CAS/version on `IProcessContextStore` — concurrent writes silently overwrite), **EXT-02** (state save + event publish not atomic — no outbox), **EXT-04** (full business records in event payloads, no redaction policy), **EXT-07** (`Map*` endpoints return `IEndpointRouteBuilder`, so `RequireAuthorization()` can't compose).

New open questions: **Q-006** (analyzer disposition — keep DI-hygiene suite vs prune), **Q-007** (EXT-01/02 scope for 1.0 — the largest architectural decision in the set), **Q-008** (ship KAL2xxx consumer analyzer package or keep repo-only).

## What Changed in v2.0

A second, code-verified analysis pass was run per `docs/PRERELEASE_PROMPT.md` (phases 0–15, six review threads covering all `src/**`, `tests/**`, `tools/**`, build/CI, and docs). ~90 raw findings were deduplicated and merged into the review set.

### v1 findings re-validated

| Item | v2 Verdict |
|------|-----------|
| CR-001 HTTP Abstractions layering | **De-scoped** — multi-transport is already the documented model; real fix = 3 boundary leaks (HP-011, MP-013, LP-010) |
| CR-002 Build hardening | **Partially incorrect** — Source Link, snupkg, symbols, XML docs, deterministic builds ARE configured; real gaps are lock files, pack step, coverage gate, warnings-as-errors scope |
| CR-003 Versioning | **Confirmed + sharpened** — `1.0.0` hard-coded in `src/Directory.Build.props` |
| HP-001 Extension points | **Confirmed, expanded** — most seams verified earned; questionable surface narrowed |
| HP-002 Dispatch docs | **Partially addressed** — order now documented; precedence untested |
| HP-003 Analyzer docs | **Confirmed, expanded** — KAL1010/1011 undocumented; tools/analyzers has no guide |
| HP-004 Mapping scattered | **Superseded** — referenced deleted `Kaleido.AspNetCore`; folded into MP-007/013/020, LP-004 |

## Files

- **REVIEW_FINDINGS.md** — v2.1 merged findings (9 release blockers + 6 EXT blockers, 14 high, 26 medium incl. EXT items, 15 low, 6 analyzer proposals, consolidated breaking-change list, final assessment + scores)
- **REVIEW_TRACKER.yaml** — v2.1 tracking (per-item v2 notes, corrected statuses, Q-001–008, EXT-01–019)
- **REVIEW_INTEGRATION.md** — workflow guide
- **REVIEW_QUICK_REF.md** — v2.1 at-a-glance card

## Headline Findings

1. Root docs + quickstart describe a deleted project — first consumer experience is a compile error
2. Published contracts (exception→status mapping, correlation echo, cancellation signals) have near-zero test coverage
3. `ProcessRuntime` violates the documented one-signal-per-cancellation rule
4. Malformed correlation header → 500, not 400
5. Production-unsafe defaults (events discarded, unbounded in-memory state) surface only via one-time warnings
6. Typos/name drift that ship permanently at 1.0: `KaleidoProcessStepREgistry`, `UseSqliteContextStore` vs `UseSqliteProcessContextStore`

## Next Steps

1. Schedule design review (agenda in `REVIEW_TRACKER.yaml` → `review_milestones`)
2. Fix release blockers in Week 1 — most are Small complexity
3. Run `dotnet build Kaleido.slnx` + `dotnet test Kaleido.slnx` to baseline before changes
