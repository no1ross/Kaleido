# Kaleido Review: Quick Reference Card (v2.1)

## Review Documents

| File | Purpose | Use When |
|------|---------|----------|
| **REVIEW_FINDINGS.md** | Primary findings + decisions (v2.0, code-verified) | Team discussions, decision-making |
| **REVIEW_TRACKER.yaml** | Structured tracking config (v2.0) | PM tools, sprint planning, status updates |
| **REVIEW_INTEGRATION.md** | Integration guide + workflow | Understanding how to use docs |
| **REVIEW_STATUS.md** | Summary + next steps | Quick orientation |

**v2.0:** second-pass analysis per `docs/PRERELEASE_PROMPT.md` merged in. v1 findings re-validated: CR-002 partially incorrect (Source Link/symbols ARE configured), HP-004 superseded (referenced deleted project), CR-001 de-scoped to boundary-leak fixes.

**v2.1:** merged two external reviews (`cleanup chatgpt ent.md`, `cleanup copilot.md`). Both used a compressed Repomix export — re-validated; 19 new EXT findings added, most external items deduplicated into existing IDs. Two external claims verified in source: sync `Count()`/`ToList()` behind async query APIs; `Kaleido.Analyzers` packable with zero rules.

---

## Findings at a Glance

### 🔴 RELEASE BLOCKERS

```
CR-004  Docs describe removed Kaleido.AspNetCore; quickstart calls AddAspNetCore()
CR-005  ExceptionMiddleware exception→response contract: 2 tests, ~zero coverage
CR-006  ObservabilityMiddleware correlation echo: 1 trivial test
CR-007  Duplicate Canceled() in ProcessRuntime violates AGENTS.md invariant (+no tests)
HP-005  UseSqliteContextStore vs documented/warning-named UseSqliteProcessContextStore
HP-007  Malformed correlation GUID header → 500 (should be 400)
HP-009  Unsafe defaults: events discarded + unbounded in-memory store (warn-only)
HP-010  Moq 4.20 SponsorLink; redundant direct SQLitePCLRaw reference
HP-015  Endpoint-name typo "KaleidoProcessStepREgistry" ships permanently at 1.0
CR-003  Versioning: all packages hard-code 1.0.0; no strategy
EXT-01  IProcessContextStore: no CAS/version/idempotency — concurrent writes overwrite
EXT-02  State save + event publish not atomic (no outbox)
EXT-03  Sync Count()/ToList() behind async query APIs (VERIFIED defect)
EXT-04  Full business records in event payloads; no redaction policy
EXT-05  Kaleido.Analyzers packable but contains ZERO rules (VERIFIED)
EXT-07  Map* return IEndpointRouteBuilder — RequireAuthorization can't compose
```

### � HIGH (address pre-1.0)

```
HP-006  Docs teach obsolete AddAssembly/AddQueryable/AddProcessor model
HP-008  ServiceName↔client-key route-prefix contract undocumented (silent 404s)
HP-011  KaleidoClientOptions (HTTP config) lives in transport-agnostic core
HP-012  GetCallingAssembly fallback nondeterministic (NoInlining needed)
HP-013  AddHttpClients silently skips bad config; scoped factories defeat cache
HP-014  ValidatePage: silent non-pageable acceptance, wrong message, Offset unchecked
HP-016  Step exceptions → 200 generic outcome; Code lost; client ignores error body
HP-017  SQLite store: 2 shallow tests, no concurrency coverage
HP-018  ExecutionProcessorTests over-mocked (1395 lines, strict, brittle)
HP-001  Extension-point use cases still undocumented (now verified-earned mostly)
HP-002  Dispatch order documented but precedence untested
HP-003  ANALYZERS.md missing KAL1010/1011
```

### 🟡 MEDIUM (26 items incl. EXT-08..EXT-19 — see FINDINGS)

Key: MP-005 MapProcessor throws for query-only svc · MP-007 delete GuardQueryAsync · MP-009 hot-path reflection · MP-013 ProcessResponseFactory in wrong assembly · MP-014 registry URL SSRF surface · MP-015 SQLite telemetry unregistered · MP-016 public-surface cleanup · MP-018 4-pass registry collapse · MP-019 contract/invariant test gaps · MP-020 FieldLookup×3 · MP-021 shadowed contract records · MP-022 stale dirs/artifacts · MP-023 doc-map gaps · MP-024 lock files/pack/coverage gate · MP-025 internal client APIs · MP-026 registry-200 contract + step-name drift

### 🔵 LOW (15 items) + 6 analyzer proposals (KAL0020/21 internal; KAL2001–2004 consumer pkg)

---

## Verified-Clean (no findings)

- Layering correct (core transport-free; Http thin; no cycles)
- No secrets, no unsafe deserialization, EF LINQ only, header sanitization consistent
- OCE handling correct everywhere except `ProcessRuntime`
- No raw `InvalidOperationException`, no empty catches
- OTel two-tier API well-designed
- Moq aside: pinned central versions, deterministic builds, SourceLink+snupkg present

---

## Scores (v2)

| Dimension | Score | Note |
|-----------|-------|------|
| Architecture | 8/10 | Verified; small boundary leaks |
| Simplicity | 6/10 | Registry/metadata over-built |
| Developer Experience | 5/10 | Quickstart doesn't compile |
| API Design | 6/10 | Typo'd const; internal-only client APIs |
| Maintainability | 6/10 | Duplication + brittle mega-tests |
| Testability | 4/10 | Published contracts untested |
| Observability | 7/10 | Good design; 1 invariant violation |
| Documentation | 4/10 | Root docs describe deleted project |
| Release Readiness | 5/10 | ~75% ready |

---

## Open Questions

| Q | Question | Impact |
|---|----------|--------|
| Q-001 | Non-HTTP transports future? | Medium (informs decoupling depth) |
| Q-002 | Real IEventPublisher consumers? | High |
| Q-003 | MapRegistry always-200 deliberate? | Medium |
| Q-004 | Adopt or retire SutFixture? | Medium |
| Q-005 | Remove Assemblies calling-assembly fallback? | High |
| Q-006 | Keep DI-hygiene analyzers or prune to correctness-only? | Medium |
| Q-007 | EXT-01/02 scope: implement CAS+outbox now or design-only? | **Critical** |
| Q-008 | Ship KAL2xxx consumer analyzer pkg or repo-only? | Medium |

---

## Timeline

- **Week 1:** decisions + all release blockers (docs purge, rename, 400 fix, typo, Moq, contract tests, OCE fix)
- **Week 2:** HP items (docs model, boundary moves, fail-fast, validation, error taxonomy, tests)
- **Week 3:** MP cleanup + RELEASE.md/versioning + pipeline hardening + full `dotnet test Kaleido.slnx` green
