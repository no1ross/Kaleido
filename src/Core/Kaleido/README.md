# Kaleido

Kaleido is a framework for exposing business capabilities through consistent, discoverable contracts.

Most business applications contain two fundamental types of capabilities:

- Information that can be retrieved
- Actions that can be performed

Traditional applications often expose these capabilities through custom APIs, custom contracts, custom documentation, and custom user experiences. As applications grow, it becomes increasingly difficult for consumers to discover available functionality and understand how those capabilities should be used.

Kaleido provides a consistent model for exposing business capabilities while allowing developers to focus on business functionality rather than framework infrastructure.

---

## The Problem

Developers frequently spend significant effort creating:

- API endpoints
- Request contracts
- Response contracts
- Search functionality
- Filtering functionality
- Sorting functionality
- Validation
- Documentation
- Consumer integration guidance

Even when solving similar business problems, these implementations are often inconsistent across applications and teams.

This creates friction for both developers and consumers.

---

## The Goal

Kaleido attempts to standardize how business capabilities are exposed.

Rather than requiring consumers to have prior knowledge of implementation details, Kaleido promotes:

- Discoverable capabilities
- Explicit contracts
- Metadata-driven validation
- Consistent consumer experiences
- Standardized patterns for exposing business functionality

The objective is to reduce the effort required to expose business capabilities while making those capabilities easier to understand and consume.

---

## Business Capabilities

Kaleido models business capabilities through two complementary concepts.

### Queryable

Queryable exposes business information.

Examples:

- Products
- Customers
- Orders
- Prior Authorizations

Queryable enables consumers to discover and retrieve information through consistent support for:

- Search
- Filtering
- Sorting
- Paging

Queryable metadata allows consumers to understand available fields, query capabilities, validation requirements, and supported operations without requiring external documentation.

---

### Process

Process exposes business actions.

Examples:

- Add Item To Cart
- Submit Order
- Approve Prior Authorization
- Request Additional Information

A Process represents something the business can do.

Process metadata allows consumers to discover available actions, understand required inputs, validate requests, and understand the relationships between business actions.

---

## Why Separate Queryable and Process?

Business information and business actions solve different problems.

For example:

```text
Find Products