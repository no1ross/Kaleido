This is exactly the kind of thing I would want as the authoritative document for Kaleido going forward. Since you've now made enough architectural decisions that future conversations risk re-litigating old ideas, I'd intentionally make this document opinionated and treat it as the source of truth until explicitly amended.

# Kaleido Architecture

> Version: 1.0 Baseline
>
> This document captures the currently accepted architecture, design principles, and implementation decisions for the Kaleido framework.
>
> The purpose of this document is to:
>
> - Provide a single architectural source of truth.
> - Prevent previously-settled decisions from being repeatedly revisited.
> - Clearly define responsibility boundaries.
> - Guide future implementation and refactoring.
> - Document deferred ideas separately from approved architecture.

---

# 1. Executive Overview

Kaleido is a metadata-driven framework family designed to help organizations expose data and orchestrate business processes without coupling consumers to implementation details.

The framework currently consists of two primary modules:

```text
Kaleido
├── Queryable
└── Process


Each module answers a different question:

Module	Core QuestionQueryable	What information exists and how can it be queried?
Process	Given everything known right now, what can happen next?

Although related, the modules are intentionally independent.

Queryable focuses on information discovery and retrieval.

Process focuses on business orchestration and execution.

2. Architectural Principles

The following principles apply across the entire framework.

Metadata First

Behavior should be driven by metadata and registrations rather than hardcoded implementations.

Registration Over Configuration

Consumers explicitly register assemblies and framework components.

Hidden discovery is avoided.

Strongly Typed Internals

Runtime components operate on CLR types.

Transport-specific concerns do not leak into framework internals.

Thin Orchestrators

Coordinators should coordinate.

Business logic belongs elsewhere.

Separation Of Concerns

Every component should have a single responsibility.

Validation, planning, evaluation, execution, persistence, and metadata generation remain independent.

Consumer Friendly Public APIs

Framework consumers work with records, queries, steps, and handlers.

API consumers interact only through transport contracts.

Extension Points Must Be Earned

Complex abstractions are not introduced until real usage justifies them.

3. System Boundaries
Kaleido Core

Owns:

Registration infrastructure
Assembly catalog
Builder pipeline
Shared abstractions

Examples:

IKaleidoBuilder
KaleidoBuilder
AddKaleido()
AddAssembly()


Core does not own Queryable or Process concepts.

Kaleido.Queryable

Responsible for:

Record discovery
Source discovery
Named query discovery
Query validation
Query compilation
Filtering
Searching
Sorting
Paging
Query execution
Metadata generation

Queryable remains:

Provider agnostic
Transport agnostic
Serializer agnostic

Queryable does not own:

ASP.NET Core
OpenAPI generation
JSON serialization
HTTP model binding
Kaleido.Queryable.AspNetCore

Responsible for:

HTTP contracts
Endpoint mapping
ASP.NET integration
Request normalization
Transport-specific behavior

Queryable.AspNetCore converts:

JsonElement
QueryString
HttpRequest


into:

Strongly Typed CLR Values


before Queryable executes.

Kaleido.Queryable.OpenApi

Responsible for enriching OpenAPI metadata.

It does not discover metadata itself.

Instead:

Queryable Metadata
    ↓
OpenAPI Enrichment


OpenAPI generation remains the responsibility of Swagger/Swashbuckle.

Kaleido.Process

Responsible for:

Process discovery
Planning
Execution
Evaluation
State management
Process orchestration

Process is NOT:

A workflow engine
BPMN
Camunda
BizTalk

Process is a metadata-driven orchestration framework.

4. Registration Model

Consumers initialize Kaleido through a common builder:

services
    .AddKaleido()
    .AddAssembly(typeof(SomeType).Assembly)
    .AddQueryable()
    .AddProcess();


Rules:

Assemblies must be registered explicitly.
Assemblies are shared across modules.
Duplicate registrations are ignored.
No automatic assembly scanning outside the registered assemblies.
5. Queryable Architecture
Request Model

Query contracts use node-based structures.

QueryFilterNode
 ├─ Condition
 └─ Group

QuerySearchNode
 ├─ Condition
 └─ Group


Interface-based query expressions were intentionally removed.

Execution Pipeline
Request
    ↓
Validation
    ↓
Compilation
    ↓
Source Creation
    ↓
Named Query
    ↓
Filtering
    ↓
Searching
    ↓
Sorting
    ↓
Paging
    ↓
Execution

Metadata Model

Queryable metadata includes:

RecordMetadata
FieldMetadata
NamedQueryMetadata
QueryParameterMetadata
PageableMetadata
DataTypeDescriptor


Metadata represents framework capabilities.

Registration represents runtime implementation details.

The two concepts remain separate.

Type Handling

Queryable operates exclusively on CLR values.

Supported concepts:

Strings
Booleans
Numbers
Guid
DateOnly
TimeOnly
DateTime
DateTimeOffset
TimeSpan
Enums

Queryable should never receive:

JsonElement
HttpRequest
Transport-specific types


Those are normalized before execution.

6. Process Architecture
Core Philosophy

Process answers:

Given everything that is currently known, what may happen next?

The framework owns orchestration.

Business units own business behavior.

High-Level Runtime
Runtime
    ↓
Planner
    ↓
Execution Processor
    ↓
Evaluator
    ↓
Updated State

Runtime Responsibilities

Runtime owns:

Load state
Initialize state
Reconcile state
Stamp RequestId
Build plan
Execute plan
Build final result

Runtime does not own:

Validation
Dependency checking
Evaluation logic
State mutation logic
Graph logic
Planning Pipeline
ProcessorRequest
        ↓
ExecutionCandidateBuilder
        ↓
ExecutionCandidateValidator
        ↓
ExecutionCandidateConsistencyChecker
        ↓
ExecutionPlanBuilder
        ↓
ExecutionPlannerResult


Planner primarily coordinates specialized services.

Execution Pipeline
Execute Step
       ↓
Update State
       ↓
Persist State
       ↓
Evaluate Result
       ↓
Select Next Step
       ↓
Repeat


Execution is sequential in V1.

Parallel execution is intentionally deferred.

7. Process State Model

State exists to support continuation.

State is not history.

ProcessorContext
ProcessId
LatestRequestId
State
RequiredStep
AvailableSteps
Steps


Only durable execution state is stored.

StepContext
StepName
Version
Status
LastRequestId
LastExecuted


StepContext is a snapshot.

It is not an audit record.

Persistence Rules

State must be persisted:

After initialization
After reconciliation
After successful execution
After exceptions
After cancellations

Completed steps should never execute twice.

8. State Ownership

Only one component may mutate state:

IProcessStateUpdater


Responsibilities:

Initialize()
Reconcile()
ApplyExecution()
ApplyException()
ApplyCancellation()


No other component updates state directly.

9. Evaluation Model

The graph determines:

What could happen?


The evaluator determines:

What should happen?


Evaluator decisions include:

Continue
Complete
BusinessFailure
ProcessViolation
AwaitingRequiredStep
AwaitingStepSelection


Evaluator is the authoritative progression engine.

10. Process Step Model

Every step has:

Request contract
Response contract

Primary contract:

IProcessStepHandler<TStep, TResult>


Void-style handlers use:

ProcessStepEmptyResponse


Every execution produces a response.

Responses are contracts.

Responses are not persisted in process state.

11. Eventing Model

Process state answers:

What is true now?

Events answer:

How did we get here?

Events are published through:

IEventPublisher


Consumers decide whether to:

Persist
Queue
Publish
Ignore

Process does not contain a journal subsystem.

12. Testing Strategy

Testing follows three layers.

Unit Tests

Validate framework behavior.

Use minimal assets:

StepA
StepB
StepC


Do not depend on large sample domains.

Functional Tests

Validate:

Registration
Execution
Metadata
Serialization
HTTP behavior
End-to-end framework integration
Samples

Samples demonstrate consumer usage.

Samples are documentation.

Samples do not exist to prove framework correctness.

Current Decisions

The following decisions are considered settled.

Architecture

✅ Metadata-driven framework

✅ Strongly typed internal runtime

✅ Explicit registration

✅ Thin orchestrators

✅ Queryable and Process remain separate modules

Queryable

✅ Node-based query model

✅ ASP.NET normalization layer

✅ Transport-independent runtime

✅ Metadata-driven OpenAPI enrichment

✅ Metadata and registration separated

Process

✅ Not a workflow engine

✅ Sequential execution in V1

✅ Durable persisted state

✅ Completed steps never re-execute

✅ StateUpdater is sole state mutator

✅ Evaluator is progression authority

✅ Graph provides structure only

✅ Every step has a response contract

✅ State and event history remain separate

Testing

✅ Unit tests prove framework behavior

✅ Functional tests verify integration

✅ Samples demonstrate usage

Deferred / Future

Items intentionally postponed.

Queryable
Additional providers
Async execution strategies
Executor registration model
OpenAPI package enhancements
gRPC transport layer
Process
Parallel execution
Execution priorities
Execution groups
Dynamic metadata refresh
Processor routing enhancements
Advanced event contracts
Generated endpoints
OpenAPI generation
Queryable/Process metadata integration
Shared
Common utility package
Shared metadata abstractions
Additional cross-module infrastructure

No shared package should be introduced until multiple concrete use cases exist.

Architectural Rule

If a future design discussion conflicts with this document:

Assume this document is correct.
Change the implementation.
Only change this document when an intentional architectural decision is made.

This document is the baseline architecture for Kaleido.


Honestly Andrew, I'd check this into source control immediately and treat it almost like an ADR index. The most important part is the final **Current Decisions** and **Deferred/Future** sections. Those two sections alone will save you from reopening 80% of the conversations you and I have been having over the last week.