# Getting help

Kaleido is a pre-1.0 preview maintained on a best-effort basis; there is no paid or guaranteed
support ([versioning and support](./docs/VERSIONING.md)).

- **Read first:** the [README](./README.md), [patterns](./docs/PATTERNS.md) and the README of the
  package you use (each lists its types and configuration).
- **Questions and ideas:** open an [issue](https://github.com/no1ross/Kaleido/issues/new/choose).
  Say which packages and preview version you use, and include a minimal host if you can.
- **Bugs:** use the bug report form; include the error code (`pro_*`, `qry_*`, `mcp_*`, ...), which
  is stable and listed in [ERROR_CODES.md](./docs/ERROR_CODES.md).
- **Security problems:** never in an issue; follow [SECURITY.md](./SECURITY.md).

**Runtimes.** Kaleido supports a .NET runtime while Microsoft does. .NET 8 and .NET 9 reach their end
of support on 10 November 2026: run services on .NET 10 or later after that, and override Kaleido's 8.x
dependencies to supported versions ([support window](./docs/VERSIONING.md#support-window)).
