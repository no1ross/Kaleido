# Kaleido analyzer rules

`Kaleido.Analyzers` (in `tools/analyzers`) enforces the codebase's design conventions at compile time. These are build diagnostics, not runtime error codes — runtime codes are documented in [`ERROR_CODES.md`](./ERROR_CODES.md).

Severities are configured in `.editorconfig`. In `src/` warnings are treated as errors; in `tests/` several rules are relaxed where the convention genuinely differs (e.g. `!` usage, `KAL0002` stubs).

## Source rules — `KAL0xxx`

| ID | Rule | Diagnostic message |
|---|---|---|
| KAL0001 | Static classes are reserved for extension methods — no static helper/util classes | `Static class '{0}' is reserved for extension methods — convert '{1}' to an extension method or use a non-static class` |
| KAL0002 | Throw `Kaleido*Exception` types, not general BCL exceptions (argument guards and the ASP.NET 400 contract are allow-listed) | `Throw a Kaleido*Exception instead of '{0}'` |
| KAL0003 | No `!` null-forgiving operator — use explicit null checks | `Replace '!' with an explicit null check` |
| KAL0004 | Exception types must not be records | `Exception type '{0}' is declared as a record — exception classes must remain classes` |

## Test rules — `KAL1xxx`

Scoped to unit-test projects via `.editorconfig`.

| ID | Rule | Diagnostic message |
|---|---|---|
| KAL1001 | Test fixtures are named after their subject under test: `{SutName}Tests` | `Test fixture '{0}' must be named after its subject under test ('{1}Tests')` |
| KAL1002 | The fixture's `{Sut}Tests` prefix must resolve to a real type | `Test fixture '{0}' does not map to a type named '{1}' — fixtures are named {SutName}Tests` |
| KAL1003 | The fixture's file location mirrors the SUT's location: `src/{Project}/{path}/Sut.cs` → `tests/{TestProject}/{path}/SutTests.cs` | `Test fixture '{0}' must live at '{1}' (mirroring SUT path '{2}')` |
| KAL1004 | One fixture per subject under test | `Test fixture '{0}' duplicates '{1}' — both map to SUT '{2}'` |
| KAL1005 | Test-built `ServiceProvider`s must validate scopes and validate on build | `BuildServiceProvider() must pass 'new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }'` |
