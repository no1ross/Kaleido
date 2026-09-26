# Integrating Pre-Release Review Findings into Kaleido Project

## Overview

This document explains how the comprehensive pre-release review findings are integrated into the existing Kaleido project structure and documentation.

**Related Documents:**
- [`REVIEW_FINDINGS.md`](./REVIEW_FINDINGS.md) — Main findings document (trackable, team-reviewed). **v2.1** merges a second code-verified analysis pass per [`docs/PRERELEASE_PROMPT.md`](./docs/PRERELEASE_PROMPT.md) plus two external reviews (`cleanup chatgpt ent.md`, `cleanup copilot.md`, both re-validated; new items use EXT-xx IDs); see its "Prior Findings — v1 Validation" section before acting on v1 items.
- [`REVIEW_TRACKER.yaml`](./REVIEW_TRACKER.yaml) — YAML configuration for tracking integration (v2.1; each v1 item carries a `v2_note`/`v2_status`; EXT-01–019 + Q-006–008 added)
- [`REVIEW_QUICK_REF.md`](./REVIEW_QUICK_REF.md) — At-a-glance summary
- [`REVIEW_STATUS.md`](./REVIEW_STATUS.md) — Merge status and next steps

## Document Organization

### Existing Kaleido Documentation

```
C:\Repos\Kaleido\
├── README.md                    ← Framework overview
├── ARCHITECTURE.md              ← Top-level architecture
├── AGENTS.md                    ← Contributor guide
├── docs/
│   ├── ANALYZERS.md            ← Analyzer rule documentation
│   ├── ERROR_CODES.md           ← Error code reference
│   ├── PRERELEASE_PROMPT.md     ← Review specification (phases 0–15)
│   └── archive/                 ← Historical session/design summaries
└── src/
    ├── ARCHITECTURE.md          ← Source-level architecture
    ├── AGENTS.md                ← Source contributor guide
    └── [project READMEs]
```

### New Review Documentation

```
C:\Repos\Kaleido\
├── REVIEW_FINDINGS.md           ← Team-tracked findings (Main document)
├── REVIEW_TRACKER.yaml          ← Tracking configuration
├── REVIEW_INTEGRATION.md        ← This integration guide
└── .github/
    └── REVIEW_DECISIONS.md      ← Decision log (create as decisions made)
```

## How to Use These Documents

### For Repository Maintainers

**Week 1: Decision Phase**

1. Open [`REVIEW_FINDINGS.md`](./REVIEW_FINDINGS.md)
2. Go to **Critical Findings** section
3. Schedule team meeting to review and approve decisions:
   - [CR-001] HTTP Abstractions: Multi-transport or single-transport?
   - [CR-002] Build hardening: Implement all recommendations?
   - [CR-003] Versioning: Adopt SemVer policy?

4. Record decisions in `.github/REVIEW_DECISIONS.md`:
```markdown
# Kaleido Pre-Release Review Decisions

## [2026-09-28] Decision: CR-001 HTTP Abstractions Architecture

**Decision:** Option A — Multi-transport ready (split HTTP Abstractions)

**Rationale:** 
- Future framework evolution requires transport abstraction layer
- Enables gRPC, messaging support
- Worth the 3-5 day effort pre-1.0

**Assigned To:** @engineer-name

**Timeline:** Week 3

**Status:** APPROVED
```

**Week 2: Documentation Phase**

1. Assign owners to **Recommended Actions** (Priority 2: Documentation)
2. Each owner creates/updates relevant docs:
   - [`RELEASE.md`](./RELEASE.md) ← from CR-003
   - Expand [`docs/ANALYZERS.md`](./docs/ANALYZERS.md) ← from HP-003
   - New [`docs/QUERYABLE_DISPATCH.md`](./docs/QUERYABLE_DISPATCH.md) ← from HP-002
   - New [`docs/CORRELATION_CONTEXT.md`](./docs/CORRELATION_CONTEXT.md) ← from MP-003

3. Link created docs back into [`REVIEW_FINDINGS.md`](./REVIEW_FINDINGS.md):
```
✅ [CR-003] Versioning & Release Strategy
   → Created: RELEASE.md
   → Links to: SemVer policy, release checklist, changelog format
```

**Week 3: Implementation Phase**

1. Assign engineering tasks from **Recommended Actions** (Priority 3)
2. Create GitHub issues with references:
```
Title: [CR-001] Split HTTP Abstractions into transport and HTTP projects

Body:
Related findings: REVIEW_FINDINGS.md#CR-001
Tracking: REVIEW_TRACKER.yaml
Impact: Architecture change (breaking)
Effort: 3-5 days

Description: [from REVIEW_FINDINGS.md]
Acceptance Criteria: [from recommendations]
```

3. Use branch naming convention:
```
review/cr-001-http-abstractions
review/hp-004-mapping-centralization
review/mp-002-mapper-tests
```

4. Link PRs to findings:
```
Fixes #review-cr-001
See: REVIEW_FINDINGS.md#CR-001
Tracker: REVIEW_TRACKER.yaml
```

### For Code Reviewers

When reviewing PRs related to review findings:

1. Check [`REVIEW_TRACKER.yaml`](./REVIEW_TRACKER.yaml) for:
   - Expected effort
   - Priority classification
   - Assigned owner
   - Current status

2. Validate against [`REVIEW_FINDINGS.md`](./REVIEW_FINDINGS.md):
   - Does it address the recommended fix?
   - Are all points from "Recommended Fix" section implemented?
   - Does it include tests/documentation as specified?

3. Update status in commit message:
```
review/hp-004: centralize mapping logic

- Create Kaleido.Http/Mapping/ directory
- Extract ProcessRequestMapper
- Extract ProcessResponseMapper
- Add comprehensive unit tests

Status: HP-004 implementation phase 1 of 2
See: REVIEW_FINDINGS.md#HP-004
Updates: REVIEW_TRACKER.yaml
```

### For New Contributors

When joining the Kaleido project:

1. Read [`REVIEW_FINDINGS.md`](./REVIEW_FINDINGS.md) **Executive Summary**
2. Check [`REVIEW_TRACKER.yaml`](./REVIEW_TRACKER.yaml) for **what's decided** vs **what's in-progress**
3. Reference specific findings when:
   - Proposing architectural changes → Check **Critical Findings**
   - Adding tests → Check **Medium Priority** testing items
   - Writing docs → Check **High/Medium Priority** documentation items
   - Asking "why do we do X?" → Check **Design Confirmations** section

**Example:** "Why do we enforce DI so heavily?"
→ See [`REVIEW_FINDINGS.md`](./REVIEW_FINDINGS.md#-confirmed-strong-di-enforcement) — Confirmed as intentional

## Mapping Review Findings to Existing Docs

### Documents That Will Be Enhanced

| Review Finding | Current Doc | Change | Status |
|---|---|---|---|
| CR-003 | (new) | Create `RELEASE.md` | ⏳ Pending decision |
| CR-004 | `README.md`, `ARCHITECTURE.md`, quickstart | Purge `Kaleido.AspNetCore`; make quickstart compile | 🔴 Week 1 blocker |
| HP-003 | `docs/ANALYZERS.md` | Expand with rationale sections | ⏳ Week 2 |
| HP-002 | (new) | Create `docs/QUERYABLE_DISPATCH.md` | ⏳ Week 2 |
| MP-003 | (new) | Create `docs/CORRELATION_CONTEXT.md` | ⏳ Week 2 |
| MP-004 | (new) | Create `docs/PROCESS_DEPENDENCIES.md` | ⏳ Week 2 |
| LP-001 | `src/Kaleido/README.md` | Add enum handling section | ⏳ Post-1.0 |
| LP-002 | (new) | Create `docs/PAGINATION.md` | ⏳ Post-1.0 |

### Documents That Will Remain Unchanged

- `AGENTS.md` — Contributor patterns (still accurate)
- `docs/ERROR_CODES.md` — Error reference (comprehensive and accurate)

*(v2.0 note: `README.md`, `ARCHITECTURE.md`, and `src/ARCHITECTURE.md` are no longer "unchanged" — CR-004/HP-006 require purging references to the removed `Kaleido.AspNetCore` project and the obsolete registration model.)*

## Decision Log Integration

### Creating `.github/REVIEW_DECISIONS.md`

As each decision is made, document it in a shared location:

```markdown
# Kaleido Pre-Release Review Decisions

Track all architectural and strategic decisions made during pre-1.0 review.

## Critical Decisions (CR-xxx)

### [2026-09-28] CR-001: HTTP Abstractions Architecture

**Decision:** Option A — Multi-transport ready (split into Kaleido.Transport.Abstractions)

**Decided By:** Architect + Product Manager

**Rationale:**
- Enables future gRPC, messaging transports
- Clarifies architectural boundaries
- Worth the 3-5 day effort pre-1.0

**Status:** APPROVED

**Implementation Issue:** [Link to GitHub issue]

**Implementation PR:** [Link to PR when ready]

---

### [2026-09-28] CR-002: Build Hardening

**Decision:** Implement all recommendations

**Status:** APPROVED

**Implementation Issue:** [Link to GitHub issue]

---

### [2026-09-28] CR-003: Versioning Strategy

**Decision:** Adopt SemVer with 2-release deprecation window

**Documentation:** See `RELEASE.md`

**Status:** APPROVED

**Implementation Issue:** [Link to GitHub issue]
```

## Status Tracking Integration

### Using `REVIEW_TRACKER.yaml`

The YAML configuration can be integrated with project management tools:

**GitHub Project Integration:**
```
Each item in REVIEW_TRACKER.yaml becomes a GitHub Project card:
- Assigned to: team member
- Status: PENDING_DECISION → IN_REVIEW → IN_PROGRESS → COMPLETE
- Due: [date from timeline]
- Labels: review, critical, high-priority, hp-001, etc.
```

**Sprint Planning:**
```
Week 1: Plan critical decisions (CR-001, CR-002, CR-003)
Week 2: Plan high-priority documentation (HP-002, HP-003, MP-003)
Week 3: Plan implementation (HP-004, MP-002, MP-003)
```

## Closure & Sign-Off

### Before 1.0 Release

Verify that all **release blockers** and **High** priority items are resolved.
v2.0 note: HP-004 is superseded (see MP-007/013/020); CR-002 scope corrected to MP-024.

```markdown
## Pre-1.0 Release Checklist (v2.1)

- [ ] Q-007: EXT-01/EXT-02 scope decision (CAS+outbox: implement vs design-only)
- [ ] EXT-01: IProcessContextStore concurrency contract (or documented non-goal)
- [ ] EXT-02: State save + event publish atomicity decision
- [ ] EXT-03: Async query APIs — remove sync Count()/ToList() blocking
- [ ] EXT-04: Event payload redaction/policy decision
- [ ] EXT-05: Kaleido.Analyzers — ship rules or stop packing empty package
- [ ] EXT-07: Map* endpoints composable authorization decision
- [ ] CR-001: Boundary-leak decision made and documented
- [ ] CR-002/MP-024: Lock files, pack step, coverage gate, warnings-as-errors scope
- [ ] CR-003: RELEASE.md and versioning policy published
- [ ] CR-004: Docs purged of Kaleido.AspNetCore; quickstart compiles
- [ ] CR-005/006: Error-mapping + correlation-echo contract tests
- [ ] CR-007: OCE single-signal fix + invariant tests
- [ ] HP-001: Extension points documented with use cases
- [ ] HP-002: Dispatch algorithm documented + precedence test
- [ ] HP-003: Analyzer rationale + KAL1010/1011 documented
- [ ] HP-005: UseSqliteProcessContextStore rename
- [ ] HP-007: Malformed GUID header → 400
- [ ] HP-009: Unsafe defaults documented/enforced
- [ ] HP-010: Moq/SQLitePCLRaw supply-chain fixes
- [ ] HP-015: Endpoint-name typo fixed
- [ ] MP-002: Mapper tests comprehensive
- [ ] MP-003: Correlation context documented
- [ ] MP-004: Step dependencies documented
- [ ] MP-019: Contract/invariant test gaps closed

## Go/No-Go Decision for 1.0 Release

- All critical decisions: ✅ Made
- All critical items: ✅ Complete
- All high priority items: ✅ Complete
- Documentation: ✅ Published
- Tests: ✅ Passing
- Build hardening: ✅ Verified

**Result:** APPROVED FOR 1.0 RELEASE
```

### Archival

After 1.0 release:

1. Move [`REVIEW_FINDINGS.md`](./REVIEW_FINDINGS.md) to `/docs/REVIEW_FINDINGS_1_0.md`
2. Keep [`REVIEW_TRACKER.yaml`](./REVIEW_TRACKER.yaml) for historical reference
3. Archive `.github/REVIEW_DECISIONS.md` in `/docs/`
4. Update [`README.md`](./README.md) with "1.0 Release Notes" referencing decisions

## FAQ

### Q: Should I update REVIEW_FINDINGS.md as I work?

**A:** No. REVIEW_FINDINGS.md is a snapshot of findings at review time. Instead:
1. Update `REVIEW_TRACKER.yaml` status field
2. Link your PR to the relevant finding
3. Update `.github/REVIEW_DECISIONS.md` with implementation status

### Q: What if I discover a new issue while implementing a finding?

**A:** Document it as a follow-up:
```markdown
Found during CR-001 implementation:
  - [New Issue] HTTP header validation too strict
  - Reference: REVIEW_FINDINGS.md#CR-001
  - Status: PENDING_ANALYSIS
  - Impact: Post-1.0 or 1.1?
```

### Q: Should we create GitHub issues from REVIEW_FINDINGS.md?

**A:** Yes, but only for items with clear decisions:
- ✅ Create issues for: CR-001, CR-002, CR-003, HP-001–HP-004, MP-001–MP-004
- ❌ Don't create for: LP-001–LP-003 (defer to post-1.0)
- Issue title format: `[REVIEW] {Finding ID}: {Title}`
- Link to relevant REVIEW_FINDINGS.md section in issue body

### Q: Who's responsible for keeping REVIEW_TRACKER.yaml updated?

**A:** Each task owner updates their assigned items:
- **Assigned To:** Updates `status` field as work progresses
- **Tech Lead:** Approves status changes, updates timeline
- **Project Manager:** Ensures timeline adherence, escalates blockers

## Summary

This review has been designed for **team collaboration and long-term traceability**:

1. **REVIEW_FINDINGS.md** — Authoritative findings document (version-controlled, team-reviewed)
2. **REVIEW_TRACKER.yaml** — Structured tracking for integration with project management
3. **.github/REVIEW_DECISIONS.md** — Decision log for architectural choices
4. **Related docs** — Integration with existing Kaleido documentation

**Next Steps:**

1. ✅ Commit all three review documents
2. ⏳ Schedule design review meeting (Week 1)
3. ⏳ Make critical decisions
4. ⏳ Create GitHub issues for approved findings
5. ⏳ Track progress in REVIEW_TRACKER.yaml
6. ⏳ Verify all items complete before 1.0 release

---

**Document Maintainer:** [TBD]  
**Last Updated:** [Create date]  
**Next Review:** [After implementation complete]
