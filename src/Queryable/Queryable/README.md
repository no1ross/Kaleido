# Kaleido.Queryable

Kaleido.Queryable provides a standardized model for exposing business information.

Rather than creating custom search endpoints, filtering endpoints, sorting endpoints, paging endpoints, metadata endpoints, and consumer documentation for every business entity, developers expose information through Queryables.

A Queryable provides a consistent contract for retrieving business data while exposing metadata that allows consumers and tools to discover available capabilities at runtime.

---

## The Problem

Many applications expose business information through custom APIs.

Examples:

- Products
- Customers
- Orders
- Prior Authorizations

Although these capabilities often support similar behaviors, implementations frequently differ.

Consumers must learn:

- Available endpoints
- Available fields
- Search capabilities
- Filtering capabilities
- Sorting capabilities
- Validation requirements

This information is often undocumented, duplicated, or tightly coupled to implementation details.

---

## The Goal

Queryable standardizes how business information is exposed.

The goal is to allow developers to focus on exposing business data while the framework provides:

- Consistent contracts
- Search support
- Filtering support
- Sorting support
- Paging support
- Validation
- Metadata
- Discoverability

---

## Queryable Concepts

Queryable consists of two primary concepts:

### Context

A Context represents a business information area.

Examples:

```text
Products
Customers
Orders
Prior Authorizations