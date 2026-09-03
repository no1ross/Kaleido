# Kaleido contributor guide

This is the repository-level contributor guide for Kaleido.

Kaleido is organized into three main framework areas:
- [`src/Core`](./src/Core/README.md) for foundational bootstrap, shared abstractions, metadata primitives, eventing, correlation context, and thin ASP.NET Core support
- [`src/Queryable`](./src/Queryable/README.md) for discoverable information retrieval and query metadata/execution
- [`src/Process`](./src/Process/README.md) for discoverable business actions, durable execution state, and step orchestration

Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) first for the top-level repository model. Then read the subsystem docs for the area you are changing.

## How to navigate the repo

### Repository-level docs
- [`README.md`](./README.md)
- [`ARCHITECTURE.md`](./ARCHITECTURE.md)
- [`AGENTS.md`](./AGENTS.md)

### Core docs
- [`src/Core/README.md`](./src/Core/README.md)
- [`src/Core/ARCHITECTURE.md`](./src/Core/ARCHITECTURE.md)
- [`src/Core/AGENTS.md`](./src/Core/AGENTS.md)

### Queryable docs
- [`src/Queryable/README.md`](./src/Queryable/README.md)
- [`src/Queryable/ARCHITECTURE.md`](./src/Queryable/ARCHITECTURE.md)
- [`src/Queryable/AGENTS.md`](./src/Queryable/AGENTS.md)

### Process docs
- [`src/Process/README.md`](./src/Process/README.md)
- [`src/Process/ARCHITECTURE.md`](./src/Process/ARCHITECTURE.md)
- [`src/Process/AGENTS.md`](./src/Process/AGENTS.md)

## Repo-level boundaries

### Core
Core owns shared substrate concerns:
- bootstrap and builder state
- shared abstractions
- metadata primitives
- correlation context
- shared eventing abstractions
- thin ASP.NET Core infrastructure

### Queryable
Queryable owns discoverable business information concerns:
- query context and view registration
- query validation and execution
- query metadata publication
- query-focused ASP.NET Core transport adaptation

### Process
Process owns discoverable business action concerns:
- process-step registration
- planning and execution
- durable process state
- action metadata publication
- process-focused ASP.NET Core transport adaptation

## General contributor rules

- Keep concerns in the correct subsystem.
- Do not move capability-specific behavior into Core unless it is truly reusable and cross-cutting.
- Keep transport layers thin.
- Prefer explicit registration and discoverability over hidden behavior.
- Match documentation to the actual code and runtime behavior.
- When you change contracts or metadata semantics, also review the downstream docs and tests for the affected subsystem.

## Documentation rules

- Root docs should explain how the framework areas fit together.
- Parent subsystem docs should explain subsystem boundaries and mental models.
- Project READMEs should explain what lives in that project specifically.
- Contributor guides should focus on invariants, boundaries, and what not to change casually.

## Tests and samples

Tests and samples are important navigation aids:
- [`tests/AGENTS.md`](./tests/AGENTS.md)
- [`samples/PriorAuth/AGENTS.md`](./samples/PriorAuth/AGENTS.md)

Use samples to understand intended consumer usage.
Use tests to understand behavioral expectations and invariants.

## Rule of thumb

- If the concern is foundational and reused broadly, it may belong in Core.
- If the concern is about discovering and retrieving information, it likely belongs in Queryable.
- If the concern is about discoverable actions, stateful execution, or next-step guidance, it likely belongs in Process.
