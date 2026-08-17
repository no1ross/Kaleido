# Kaleido.Process

Kaleido.Process provides a standardized model for exposing business actions.

Rather than creating custom command endpoints, validation mechanisms, execution contracts, documentation, and consumer integrations for every business operation, developers expose business actions through Processes.

A Process provides a consistent contract for executing business actions while exposing metadata that allows consumers and tools to discover available capabilities at runtime.

---

## The Problem

Most business applications contain actions that change business state.

Examples include:

- Add Item To Cart
- Submit Order
- Cancel Order
- Approve Prior Authorization
- Request Additional Information

These operations are often implemented as custom APIs with independently designed request contracts, validation rules, documentation, and consumer experiences.

As systems grow, consumers must understand:

- Which actions exist
- Required inputs
- Validation requirements
- Execution order
- Available actions
- Action dependencies

This information is frequently undocumented, duplicated, or embedded within application code.

---

## The Goal

Process standardizes how business actions are exposed.

The goal is to allow developers to focus on business behavior while the framework provides:

- Consistent execution contracts
- Validation
- Metadata
- Discoverability
- Consumer guidance

Process makes business actions easier to expose, understand, and consume.

---

## Process Concepts

A Process represents something the business can do.

Examples:

```text
Add Item To Cart

Submit Order

Approve Prior Authorization

Request Additional Information
```

Each Process is composed of one or more Steps.

A Step represents a specific business action that can be executed.

Examples:

```text
Create Cart

Add Item To Cart

Remove Item From Cart

Submit Order
```

Process metadata describes:

- Available Steps
- Required Inputs
- Validation Requirements
- Step Dependencies
- Execution Contracts

---

## Why Use Process?

Business actions are often more complex than simply executing an API.

Consumers need to understand:

- What actions are available
- When actions are available
- What information is required
- How requests should be validated

Process provides a consistent way to expose this information while maintaining clear business contracts.

---

## Validation

Process supports metadata-driven validation.

Validation metadata may define:

- Required values
- String length constraints
- Range constraints

Requests can be validated before execution, providing immediate feedback when required inputs are missing or invalid.

Validation protects business contracts while improving the consumer experience.

---

## Discoverability

A core goal of Process is discoverability.

Consumers should not be required to inspect source code, reverse engineer APIs, or search through documentation to determine which business actions are available.

Process metadata allows consumers to discover:

- Available Processes
- Available Steps
- Input Fields
- Validation Requirements
- Execution Capabilities
- Step Relationships

Consumers can understand business contracts without requiring prior knowledge of implementation details.

---

## Metadata

Metadata is a first-class concept within Process.

Metadata is used to describe:

- Business Actions
- Input Fields
- Validation Requirements
- Execution Contracts
- Dependencies
- Availability Rules

Metadata is intended to guide consumers and tooling.

It provides a consistent mechanism for understanding how business actions should be executed and validated.

---

## Example

Consider a simple ordering process:

```text
Create Cart
    ↓
Add Item To Cart
    ↓
Submit Order
```

Each step exposes:

- Required inputs
- Validation requirements
- Execution metadata

Consumers can discover and execute these actions through a consistent contract.

---

## Long Running Processes

Some business actions complete immediately.

Others may span multiple stages and participants.

Examples include:

```text
Prior Authorization

Claims Processing

Enrollment

Case Management
```

Process provides a consistent model for exposing both simple and complex business actions while maintaining the same execution and metadata experience.

---

## Consumer Experience

Process metadata can be used to build consistent consumer experiences.

Examples include:

- Validation experiences
- Documentation tools
- Process explorers
- Administrative applications
- Business user applications

Process metadata is intended to guide consumers and tooling rather than automatically generate applications.

---

## Documentation

Additional documentation is available within the `/docs/process` folder.

Suggested reading:

- Process Overview
- Creating a Process
- Creating a Step
- Metadata
- Validation
- Dependencies
- Process State