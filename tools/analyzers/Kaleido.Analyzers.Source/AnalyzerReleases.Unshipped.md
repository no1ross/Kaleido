### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-----------------------------
KAL0001 | Kaleido.Design | Warning | Static classes are for extension methods only
KAL0002 | Kaleido.Design | Warning | Do not throw general BCL exception types
KAL0003 | Kaleido.Design | Warning | Do not use the null-forgiving operator
KAL0004 | Kaleido.Design | Error | Exception types must not be records
KAL0005 | Kaleido.Design | Warning | Service-like types must not be static
KAL0006 | Kaleido.Design | Warning | Do not new up DI-registered implementations
KAL0007 | Kaleido.Design | Warning | No service locator — resolve via constructor injection
KAL0008 | Kaleido.Design | Warning | Inject service abstractions, not concrete implementations
KAL0009 | Kaleido.Design | Warning | Services must use constructor injection only
KAL0010 | Kaleido.Design | Warning | Injected dependencies must be retained safely
KAL0011 | Kaleido.Design | Warning | DI constructors must not perform work on injected dependencies
KAL0012 | Kaleido.Design | Warning | Do not manually instantiate infrastructure dependencies
KAL0013 | Kaleido.Design | Warning | Do not dispose container-owned dependencies
KAL0014 | Kaleido.Design | Warning | Singleton registrations must not capture scoped services
KAL0015 | Kaleido.Layout | Warning | Interface must live in the same file as its implementation
KAL0016 | Kaleido.Design | Warning | MapGet/MapPost endpoints in Kaleido.Http must call .WithTags()
KAL0017 | Kaleido.Design | Warning | Endpoint tags must include "Kaleido"
KAL0018 | Kaleido.Design | Warning | Public API members must not expose mutable collection types
KAL0019 | Kaleido.Design | Warning | Async methods must accept a CancellationToken parameter
