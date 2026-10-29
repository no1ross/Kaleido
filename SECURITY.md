# Security policy

## Reporting a vulnerability

**Do not report security problems in public issues, pull requests or discussions.**

Report them privately through GitHub: on the repository's **Security** tab, choose
**Report a vulnerability** ([private vulnerability reporting](https://github.com/no1ross/Kaleido/security/advisories/new)).
Only the maintainers see the report, and the fix is coordinated in a private advisory. The report
reaches every maintainer account, the backup account included ([maintainers](./MAINTAINERS.md)), so
it is not lost if one account is unavailable.

Please include:

- the affected package(s) and version (the `1.0.0-preview.*` number, or the commit);
- what an attacker can do, and under which configuration (for example `AuthorizationMode`,
  the MCP tool listing, the store in use);
- steps or a minimal host that reproduce it.

## Supported versions

Kaleido is in pre-1.0 preview ([versioning](./docs/VERSIONING.md)). Fixes are made on `master`
and ship in the next preview; earlier previews are not patched.

| Version | Supported |
|---|---|
| latest `1.0.0-preview.*` | yes |
| earlier previews | no: update to the latest preview |

## Disclosure

Reports are handled as coordinated disclosure: the maintainers confirm the problem, prepare a fix
privately, and publish a GitHub security advisory when the fix is available, crediting the reporter
unless they ask not to be named. Please keep the details private until the advisory is published.

No response times are promised while Kaleido is in preview.

## Scope

In scope: the packages built from `src/` (`Kaleido`, `Kaleido.Http`, `Kaleido.Http.Abstractions`,
`Kaleido.Http.Client`, `Kaleido.Http.Mcp`, `Kaleido.Observability.OpenTelemetry`,
`Kaleido.Provider.SQLite`) and the analyzers in `tools/`.

Out of scope:

- the samples ([Kaleido-Samples-PriorAuth](https://github.com/no1ross/Kaleido-Samples-PriorAuth), [Kaleido-Samples-ECommerce](https://github.com/no1ross/Kaleido-Samples-ECommerce)), which run with development sign-in and are not meant to be deployed;
- behavior Kaleido documents as the host's responsibility: authentication, case-level access
  ([process ids are bearer handles](./docs/AUTHORIZATION.md#threat-model-the-process-id-is-a-bearer-handle)),
  production storage ([`Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md) is a
  reference implementation).
