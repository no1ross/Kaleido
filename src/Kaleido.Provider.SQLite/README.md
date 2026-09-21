# Kaleido.Provider.SQLite

This project provides a SQLite-backed durable process state store. It replaces the default in-memory `IProcessContextStore` with a persistent implementation suitable for multi-request workflows and service restarts.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)

---

## What lives here

- `SqliteProcessContextStore` — SQLite-backed `IProcessContextStore` implementation
- `SqliteProcessContextStoreServiceCollectionExtensions` — `UseSqliteProcessContextStore(connectionString)` builder extension

---

## When to use this project

Use this project when:
- process state must survive service restarts
- process state must be shared across multiple application instances
- a process spans multiple HTTP requests and in-memory storage is not sufficient

The default in-memory store registered by `AddProcessor(...)` is sufficient for:
- local development
- single-request processes
- unit and functional tests

---

## Registration

Call `UseSqliteProcessContextStore(...)` after `AddProcessorAspNetCore()`:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddProcessor(options =>
    {
        options.Name = "my-processor";
        options.DisplayName = "My Processor";
        options.Version = "1.0.0";
    })
        .AddProcessorAspNetCore()
        .UseSqliteProcessContextStore("Data Source=my-process.sqlite");
```

This registers `SqliteProcessContextStore` as the `IProcessContextStore` implementation, replacing the default in-memory store.

---

## What this project does NOT do

This project does not contain:
- the Process runtime itself (see [`Kaleido`](../Kaleido/README.md))
- HTTP endpoint publication (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- other persistence providers (EF Core, Redis, etc.)

---

## Where to look

- `SqliteProcessContextStore.cs` — SQLite implementation of `IProcessContextStore`
- `SqliteProcessContextStoreServiceCollectionExtensions.cs` — `UseSqliteProcessContextStore(...)` registration
