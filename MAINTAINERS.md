# Maintainers

Who maintains Kaleido, what each role may do, and where the project's accounts are held (#467).

## Today: one maintainer, with a backup account

| Account | Role |
|---|---|
| [@no1ross](https://github.com/no1ross) | maintainer: repository owner |
| [@andrew-ross-elv](https://github.com/andrew-ross-elv) | backup account of the same maintainer |

Kaleido has **one maintainer**. The second account belongs to the same person: it keeps the project
operable if one login or its second factor is lost, but it is not a second person. If the
maintainer is unavailable, nobody else can merge, approve a release or answer a security report.
Weigh that when you depend on Kaleido; the project is in preview ([versioning](./docs/VERSIONING.md)).

## Roles

Each right is held by every maintainer account:
- **Review and merge:** listed in [`.github/CODEOWNERS`](./.github/CODEOWNERS), so every pull
  request requests their review; a maintainer approves every agent-authored pull request before it
  merges ([contributing](./CONTRIBUTING.md#pull-requests-written-by-ai-agents)).
- **Release approval:** a required reviewer of the `release` environment; nothing is published
  without that approval ([releasing](./RELEASING.md)).
- **Security response:** receives private vulnerability reports and coordinates the fix in a
  private advisory ([security policy](./SECURITY.md)). The backup account is the backup contact.

## Where the accounts are held

No secret is stored in the repository. The project's accounts and settings, all administered by the
maintainer:
- the GitHub repository settings: branch protection, the `release` environment and its reviewers,
  private vulnerability reporting;
- the SonarCloud organization (`no1ross_Kaleido`);
- the GitHub Packages feed, and the nuget.org account that owns the `Kaleido` package IDs (trusted
  publishing; no stored API key, [releasing](./RELEASING.md)).

## Adding or removing a maintainer

The maintainer adds a person who has contributed reviewed changes and agrees to the roles above,
by granting every right in [Roles](#roles) and adding them here and to `CODEOWNERS` in one pull
request. A maintainer leaves the same way, rights removed in the same change. With a second person,
this page names the security-response owner and its backup, and no single person holds release
approval alone.
