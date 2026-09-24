Kaleido Pre-Release Architecture & Code Review

Please perform a comprehensive pre-release architecture and code review of the Kaleido codebase and create a cleanup.md document containing all findings.

Review Constraints

DO NOT IMPLEMENT CHANGES DURING THIS REVIEW.

Your first deliverable is cleanup.md only.

Produce findings, recommendations, priorities, and proposed solutions.

Do not refactor code until the findings have been reviewed and approved.

Assume the framework is over-engineered.

Actively look for things that can be removed.

Assume extension points must be earned.

Every extension point must justify its existence with a real-world use case.

Assume future maintainers are unfamiliar with the framework.

Optimize recommendations for developer discoverability and comprehension.

Evaluate the framework as if the original author were unavailable after release and future maintenance would be performed entirely by other developers.

Please exclude /samples/** from the review.

General Guidance
Review all *.md files.
Review all content under docs/.
Review all analyzers and associated documentation to understand project conventions, architectural intent, and coding standards.
Assume documentation reflects current intent but may not accurately reflect the implementation.
Identify and document all discrepancies between documentation and implementation.
Treat the repository as a pre-release framework approaching a public 1.0 release.
Optimize recommendations for long-term maintainability, simplicity, discoverability, consumer experience, and release readiness.
Assume every public API introduced today may need to be supported for the next five years.
Evaluate changes through the lens of framework consumers rather than framework authors.
Additional Review Requirements
Favor deleting code over adding code whenever possible.
Favor simplifying abstractions over extending abstractions.
Favor explicit behavior over magic behavior.
Favor consumer experience over framework cleverness.
Favor maintainability over configurability.
Favor discoverability over architectural purity.
Question every extension point, registry, abstraction, factory, strategy, metadata construct, and configuration option.
Do not assume complexity is justified simply because it already exists.
Identify opportunities to reduce the public surface area.
Identify opportunities to reduce cognitive load.
Identify opportunities to reduce framework-specific concepts.
Identify anything that would make adoption more difficult for a new team.
Kaleido-Specific Principles

Evaluate all recommendations against the following principles:

Extension points must be earned.
Every abstraction must justify its existence with a real-world use case.
Every public API should feel obvious to a senior .NET developer.
Interfaces and implementations should be colocated when tightly coupled.
Related records should be grouped together when it improves discoverability.
Internal implementation details should not be exposed unnecessarily.
The framework should optimize for simplicity, readability, and comprehension.
Framework conventions should be learnable quickly.
The source code should be understandable without extensive documentation.
Developer experience should take precedence over architectural cleverness.
Required Output

Create a cleanup.md document containing:

Executive Summary

Provide a high-level assessment of the framework.

Critical Findings

Issues that should be addressed before a public release.

High Priority Findings

Important issues that should be resolved before 1.0 if possible.

Medium Priority Findings

Valuable improvements that are not release blockers.

Low Priority Findings

Minor improvements and future considerations.

Recommended Release Blockers

List any issues that should prevent a 1.0 release.

Recommended Pre-1.0 Improvements

List improvements that should ideally be completed before release.

Recommended Post-1.0 Improvements

List improvements that can reasonably wait until after release.

Finding Format

For every finding include:

Severity (Critical / High / Medium / Low)
Category
Description
Why It Matters
Recommended Fix
Estimated Complexity (Small / Medium / Large)
Breaking Change Risk (None / Low / Medium / High)
Can Be Automated (Yes / No)
Review Phases

For every phase below, generate a checklist of findings and recommendations.

Phase 0 - Documentation & Alignment Review

Review:

README.md
docs/*
architecture documentation
analyzer documentation
XML documentation
code comments

Verify:

Documentation matches implementation.
Examples remain valid.
Terminology is consistent.
Architectural diagrams remain accurate.
Documentation reflects the current project structure.
Documentation reflects current APIs.
Documentation does not describe removed concepts.
Analyzer documentation matches analyzer behavior.

Identify:

Documentation gaps.
Conflicting guidance.
Missing examples.
Outdated references.
Inconsistent terminology.
Phase 1 - Modernize Language Features

Review and recommend:

File-scoped namespaces.
Primary constructors.
Collection expressions.
Nullable reference types.
Required members.
Sealed classes where appropriate.
Readonly fields where appropriate.
Readonly structs where appropriate.
Modern C# patterns.
Proper async usage.
Implicit usings.
Access modifier consistency.
Strongly typed options versus magic strings.
Elimination of unnecessary null-forgiving operators.

Provide modernization opportunities while preserving behavior.

Phase 2 - Remove Technical Debt

Identify:

Dead code.
Dead registrations.
Dead analyzers.
Duplicate services.
Duplicate extension methods.
Duplicate validation logic.
Duplicate mapping logic.
Duplicate exception handling.
Duplicate logging patterns.
Stale code paths.
Legacy implementation leftovers.

Group findings by category.

Phase 3 - Dependency Hygiene

Review:

Project dependencies.
Namespace dependencies.
Layering violations.
Coupling concerns.
Circular dependencies.
Transitive dependency abuse.
Infrastructure leakage into abstractions.
Unnecessary package references.
Dependency ownership.

Identify opportunities to simplify dependencies.

Phase 4 - Observability Audit

Verify:

Structured logging.
Correlation IDs.
Tracing.
Metrics.
Health checks.
Telemetry consistency.
Exception tracking.
Consumer observability experience.

Evaluate whether consumers automatically benefit from framework observability features.

Identify observability gaps.

Phase 5 - Security Review

Review for:

Hard-coded secrets.
Connection strings.
API keys.
Sensitive configuration values.
Unsafe serialization.
Unsafe deserialization.
Missing input validation.
Missing authorization.
Sensitive data logging.
SQL injection risks.
XSS risks.
Supply chain risks.
Package vulnerabilities.

Identify security concerns and remediation recommendations.

Phase 6 - API Design Review

Review:

Naming consistency.
Generic constraints.
Discoverability.
Public contracts.
API ergonomics.
Consumer onboarding experience.
Consistency of framework conventions.
Leaky abstractions.
Over-engineering.
Unnecessary complexity.

Ask:

If this API were released tomorrow, would we regret supporting it for the next five years?

Phase 7 - Exception Strategy Review

Verify:

Exceptions are not swallowed.
No empty catch blocks.
Meaningful exception messages.
Consistent exception patterns.
Global exception handling.
Errors contain actionable information.
Framework exceptions improve troubleshooting.

Review overall error experience from a consumer perspective.

Phase 8 - Test Quality Review

Evaluate:

Test quality.
Test maintainability.
Brittle tests.
Duplicate tests.
Meaningless tests.
Over-mocked tests.
Missing critical-path tests.
Public contract tests.
Consumer-focused testing scenarios.

Focus on test quality rather than code coverage percentage.

Phase 9 - Performance Review

Review:

Allocations.
Reflection usage.
Startup cost.
Assembly scanning.
LINQ inefficiencies.
ToList abuse.
Unnecessary serialization.
Synchronous I/O.
Resource utilization.
Hot paths.
Caching opportunities.

Identify performance risks that may become future scaling problems.

Phase 10 - Framework-Specific Review
Extension Point Audit

For every extension point:

Why does it exist?
Who uses it?
Is it justified?
Can it be removed?
Can it be simplified?
Registry Audit

Review:

Registries
Registration patterns
Metadata
Discovery mechanisms
Configuration systems

Determine whether complexity solves a real-world problem or a framework-created problem.

Public Surface Area Audit

Review:

Public classes
Public interfaces
Public methods
Public records
Public extension methods

Identify APIs that should be internal.

Phase 11 - Build & CI Hardening

Review:

Warnings as errors.
Analyzer enforcement.
Deterministic builds.
Package locking.
Source Link.
Dependency validation.
Security scanning.
Code coverage gates.
Build reliability.
Release packaging.
Symbol generation.
Versioning strategy.

Provide recommendations for release pipeline hardening.

Phase 12 - Bloat Removal

Assume the framework is over-engineered.

Identify:

Classes that can be removed.
Interfaces that can be removed.
Factories that can be removed.
Extension points that can be removed.
Generic abstractions that can be removed.
Configuration systems that can be removed.
Metadata systems that can be removed.
Patterns that can be removed.

Deleting code is preferred over adding code.

Phase 13 - Code Organization Review

Review source organization and discoverability.

Identify:

Fragmented concepts.
Types that should be colocated.
Interfaces that should live with implementations.
Related records that should be grouped together.
Internal types that should be nested.
Features spread across too many files.
Source structures that increase cognitive load.

Optimize for concept-based organization rather than one-type-per-file conventions.

Phase 14 - Analyzer Opportunity Review
 
Identify opportunities for new Kaleido-specific analyzers.
 
Recommend analyzers only when they enforce meaningful architectural standards, framework usage patterns, testing conventions, dependency injection requirements, API design principles, or developer guidance that cannot be effectively enforced through existing Roslyn analyzers, EditorConfig settings, StyleCop, or Meziantou Analyzer rules.
 
For each proposed analyzer include:
 
- Analyzer ID Proposal
- Purpose
- Problem Being Solved
- Example Violation
- Severity Recommendation
- Estimated Implementation Complexity
- Expected Long-Term Value

Review all analyzers.

Determine:

Which analyzers provide significant value.
Which analyzers overlap existing tooling.
Which analyzers are too opinionated.
Which analyzers should be warnings.
Which analyzers should be errors.
Missing analyzer opportunities.
Analyzer maintenance concerns.

Evaluate how effectively analyzers enforce framework standards.

Phase 15 - Consumer Experience Review

Pretend you are a senior .NET architect evaluating Kaleido for the first time.

Identify:

Confusing concepts.
Difficult onboarding experiences.
Hidden assumptions.
Surprising behavior.
Inconsistent patterns.
Undocumented conventions.
Areas that require excessive tribal knowledge.

Evaluate the framework from the perspective of a potential adopter.

Final Assessment

At the conclusion of the review provide:

Top 10 Recommended Changes Before 1.0
Top 10 Simplification Opportunities
Top 10 Code Deletion Opportunities
Biggest Architectural Concern
Biggest Maintainability Concern
Biggest Consumer Experience Concern
Biggest Documentation Concern
Biggest Release Risk
Architecture Score (1-10)
Simplicity Score (1-10)
Developer Experience Score (1-10)
API Design Score (1-10)
Maintainability Score (1-10)
Testability Score (1-10)
Observability Score (1-10)
Documentation Score (1-10)
Release Readiness Score (1-10)

Explain every score in detail.
