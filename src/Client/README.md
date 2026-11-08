# Kaleido.Client

Kaleido client base, with no ASP.NET Core: the processor and queryable client contracts libraries call, and what a call made for a caller is (call context, purpose, on-behalf-of modes). It also owns `IKaleidoRegistryClient` and the protocol-neutral registration, heartbeat, hydrated central-registry copy and readiness check; protocol packages provide the wire client.

See the [repository README](https://github.com/no1ross/Kaleido#readme) and [ARCHITECTURE.md](https://github.com/no1ross/Kaleido/blob/master/ARCHITECTURE.md).
