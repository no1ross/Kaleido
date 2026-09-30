# Copilot Instructions

## Project Guidelines
- Kaleido pre-release review has been completed with comprehensive findings organized in three trackable documents: REVIEW_FINDINGS.md (team-reviewed findings with decisions/approvals), REVIEW_TRACKER.yaml (YAML configuration for project management integration), and REVIEW_INTEGRATION.md (guide for using findings in existing project). All findings classified as by-design or requiring confirmation. Documents support version control, team collaboration, and governance.

## AI & Code Intelligence Compatibility
- User requested AI compatibility analysis for Kaleido pre-release review. Key additions identified:
  - AI-001: Dispatch order not tested formalization
  - AI-002: .editorconfig rules for IDE/copilot style enforcement
  - AI-003: XML doc coverage gaps (60-70%) blocking IntelliSense
  - AI-004: Sync vs async IQueryContextSource confusion
  - AI-005: Correlation context invariants undocumented
  - AI-006: Missing canonical code patterns
  - AI-007: Security/constraint validation rules undocumented
- Total effort estimated at ~9-10 days across the team. These items should be integrated into REVIEW_FINDINGS.md as a new section 11 ("AI & Code Intelligence Compatibility — Developer Tools") after "Design Confirmations" and before "Final Assessment". Update the Table of Contents to reflect new section numbering (sections 11-14 instead of 10-13).
