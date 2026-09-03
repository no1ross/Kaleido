# Kaleido Architecture

This document describes the current top-level architecture of the Kaleido repository. It is the entry point for understanding how the major framework areas fit together and where responsibility boundaries live.

Kaleido is a metadata-driven framework for exposing business capabilities through consistent, discoverable contracts.

At the highest level, the repository is organized around three framework areas:

- [`Core`](./src/Core/README.md) — foundational bootstrap, shared abstractions, metadata primitives, eventing, correlation context, and thin ASP.NET Core support
- [`Queryable`](./src/Queryable/README.md) — discoverable information retrieval, query metadata, and query execution
- [`Process`](./src/Process/README.md) — discoverable business actions, durable state, step orchestration, and execution guidance

See also:
- [`README.md`](./README.md)
- [`AGENTS.md`](./AGENTS.md)

---

## 1. Architectural overview

Kaleido separates foundational infrastructure from business-capability frameworks.

### Core
Core provides the common substrate that other framework layers build on:
- bootstrap and builder state
- shared metadata/type mapping
- validation metadata mapping
- eventing abstractions
- correlation context
- thin ASP.NET Core infrastructure

Core does not define business-capability runtimes on its own.

See:
- [`src/Core/README.md`](./src/Core/README.md)
- [`src/Core/ARCHITECTURE.md`](./src/Core/ARCHITECTURE.md)

### Queryable
Queryable exposes business information through metadata-driven query contracts.

It is responsible for:
- query context and view discovery
- validation, compilation, and execution of queries
- registry metadata for discoverable information surfaces
- transport adapters for HTTP querying and metadata publication

See:
- [`src/Queryable/README.md`](./src/Queryable/README.md)
- [`src/Queryable/ARCHITECTURE.md`](./src/Queryable/ARCHITECTURE.md)

### Process
Process exposes business actions through metadata-driven steps and durable execution state.

It is responsible for:
- process-step discovery
- planning and execution
- durable process state
- registry metadata for discoverable action surfaces
- transport adapters for HTTP execution and state endpoints

See:
- [`src/Process/README.md`](./src/Process/README.md)
- [`src/Process/ARCHITECTURE.md`](./src/Process/ARCHITECTURE.md)

---

## 2. Top-level design principles

The current repository architecture follows these principles:

### Metadata first
Capabilities should be described through metadata and registrations rather than ad hoc, hardcoded integration knowledge.

### Explicit registration
Assemblies and framework components are registered intentionally. Discovery should happen from known registration input rather than hidden global scanning.

### Strongly typed internals
Runtime components should operate on CLR types and internal contracts rather than transport-specific types.

### Thin transport layers
HTTP layers should adapt requests and responses to runtime contracts, not reimplement business semantics.

### Clear subsystem boundaries
Core, Queryable, and Process should each own their respective responsibilities without leaking capability-specific concerns into the wrong layer.

---

## 3. Repository structure

### Root-level docs
- [`README.md`](./README.md) — overall framework overview
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — this document
- [`AGENTS.md`](./AGENTS.md) — repo-level contributor guide

### Source areas
- [`src/Core`](./src/Core/README.md)
- [`src/Queryable`](./src/Queryable/README.md)
- [`src/Process`](./src/Process/README.md)

### Tests
- [`tests`](./tests)
- [`tests/AGENTS.md`](./tests/AGENTS.md)

### Samples
- [`samples/PriorAuth`](./samples/PriorAuth)
- [`samples/kaleido-sample-ecommerce-ui`](./samples/kaleido-sample-ecommerce-ui)

---

## 4. Registration model

The repository follows a layered registration model.

### Step 1: Core bootstrap
Applications start with the Core bootstrap path and root builder.

### Step 2: Shared assembly registration
Assemblies are recorded on the builder and become shared registration input for higher-level frameworks.

### Step 3: Capability registration
Queryable and Process consume the shared builder state to scan, validate, construct registries, and register their own runtime services.

This keeps:
- bootstrap concerns in Core
- query concerns in Queryable
- action/orchestration concerns in Process

---

## 5. Metadata and discoverability

A central repository-level goal is runtime discoverability.

The framework exposes metadata so consumers can understand:
- what information exists
- what actions exist
- what contracts and validation rules apply
- how to navigate the available capability surface

That metadata is layered:
- Core supplies shared metadata primitives and correlation/eventing foundations
- Queryable supplies information-discovery metadata
- Process supplies action/execution metadata

---

## 6. Transport model

Transport concerns are layered under the capability frameworks rather than owned centrally by the root architecture.

- Core contains shared ASP.NET Core infrastructure and conventions
- Queryable.AspNetCore adapts query metadata and query execution to HTTP
- Process.AspNetCore adapts process metadata, execution, and state access to HTTP

This keeps transport-specific code thin and capability-specific while preserving consistent shared conventions.

---

## 7. Contributor guidance

When working in this repository:
- start with the parent subsystem docs before changing internals
- keep Core free of capability-specific behavior unless the concern is truly cross-cutting
- keep Queryable focused on discoverable information retrieval
- keep Process focused on discoverable business actions and execution state
- verify that documentation matches the code, not the other way around

For contributor-oriented guidance, see:
- [`AGENTS.md`](./AGENTS.md)
- [`src/Core/AGENTS.md`](./src/Core/AGENTS.md)
- [`src/Queryable/AGENTS.md`](./src/Queryable/AGENTS.md)
- [`src/Process/AGENTS.md`](./src/Process/AGENTS.md)

---

## 8. Where to look next

- Start with [`src/Core/README.md`](./src/Core/README.md) to understand bootstrap and shared primitives
- Read [`src/Queryable/README.md`](./src/Queryable/README.md) for discoverable information surfaces
- Read [`src/Process/README.md`](./src/Process/README.md) for discoverable action surfaces
- Use the subsystem `ARCHITECTURE.md` files for implementation-level architecture details
