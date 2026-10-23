# Contributing to Kaleido

Thanks for helping. This page is the short version for people; the full rules for code, tests and
docs are in [`AGENTS.md`](./AGENTS.md) (written for human and AI contributors alike) and the guides
it links. Everyone taking part follows the [code of conduct](./CODE_OF_CONDUCT.md).

- **Questions** go to [SUPPORT.md](./SUPPORT.md).
- **Security problems** are reported privately: see [SECURITY.md](./SECURITY.md), never a public issue.

## Set up

- Install the **.NET 9 SDK** (pinned in [`global.json`](./global.json); back to .NET 10 once it is available again, #424); the packages target
  `net8.0`, so the .NET 8 runtime is needed to run the tests.

```bash
dotnet build Kaleido.slnx
./tools/test/Run-Tests.ps1             # runs every test project (PowerShell; pwsh on Linux and macOS)
```

If a test project fails to run or reports zero tests, see [AGENTS.md](./AGENTS.md#when-a-test-project-fails-to-run-or-reports-zero-tests).

## Work starts with an issue

GitHub Issues are the system of record. Every change needs an issue, with its **done criteria
written before the work starts**. Something new found along the way gets its own issue rather than
silently widening the change ([how to classify it](./AGENTS.md#tracking-work-discovered-mid-item)).

## Commits

Every commit passes the gate on its own (a later commit fixing an earlier one does not count):

1. `dotnet build Kaleido.slnx --no-incremental`: 0 errors, **0 warnings** (warnings are errors).
2. `./tools/test/Run-Tests.ps1`: 0 failed, with a non-zero test count.
3. Changed docs: `python3 tools/docs/check-doc-symbols.py` (every Kaleido name in the docs exists).

The commit message says why, names the issue (`Fixes #N`) and records the gate result, for example
`Gate: build 0 errors / 0 warnings; tests 812 passed`.

## Pull requests

- Branch from `master`; no stacked PRs.
- Prefer few, complete PRs: one per work package, with one commit per step and a description with
  one section per topic. One PR may fix several issues (`Fixes #A`, `Fixes #B`).
- Name breaking changes in a "Breaking changes" section ([versioning](./docs/VERSIONING.md)), and add a line to the `Unreleased` section of [`CHANGELOG.md`](./CHANGELOG.md) for a change a consumer notices.
- CI must be green: build, tests, the coverage gate (95% line coverage of `src/`), the documentation
  checks, Sonar and CodeQL.
- New public types and members carry XML documentation (the build enforces it).
- A public API change updates the package's `PublicAPI.Unshipped.txt` (the build fails otherwise:
  RS0016/RS0017; the IDE code fix writes the line). The diff is the API review
  ([public API baseline](./docs/VERSIONING.md#the-public-api-baseline)).

### Pull requests written by AI agents

Agents may author changes, but **a maintainer reviews and approves every agent-authored pull request
before it is merged**, like any other contribution. The author (human or agent) states in the PR
what was verified and how; an agent's claims about behavior are checked, not assumed.

## Review and ownership

The maintainers listed in [`.github/CODEOWNERS`](./.github/CODEOWNERS) review and merge.

## License

By contributing you agree that your contribution is licensed under the
[Apache License 2.0](./LICENSE), the license of this repository.
