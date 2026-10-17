## What and why

<!-- One section per topic: what changed and why. -->

Fixes #

## Breaking changes

<!-- Public API, wire contracts, error codes, configuration keys or telemetry names; or "None". -->

## Verification

- [ ] Every commit passes the gate: `dotnet build Kaleido.slnx --no-incremental` (0 errors, 0 warnings) and `dotnet test Kaleido.slnx --no-build --` (0 failed); the result is in each commit message.
- [ ] Docs changed: `python3 tools/docs/check-doc-symbols.py` passes, and the docs match the code.
- [ ] Sample UI changed: `npm run build` and `npm test` pass in its folder.
- [ ] New public types and members are documented.
- [ ] Authored by an AI agent: what was verified and how is stated above (a maintainer reviews before merge, see [CONTRIBUTING](../CONTRIBUTING.md)).
