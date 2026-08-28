## Local tooling
- Use `podman`, not `docker`
- When validating compose/container workflows, run `podman compose ...`
- Do not suggest `docker` commands unless explicitly asked

## PriorAuth layout
- `samples/PriorAuth/Compose/` contains local orchestration files
- `samples/PriorAuth/priorauth-ui/` is the Angular frontend
- `samples/PriorAuth/*/` folders are ASP.NET Core services targeting .NET 8
- `samples/PriorAuth/*.Artifacts/` projects contain sample contracts/artifacts

## Verification
- For backend changes, prefer `dotnet build` on the affected project
- Run `dotnet test` for any affected backend test projects
- For UI changes, run from `samples/PriorAuth/priorauth-ui`:
  - `npm test`
  - `npm run build`
- If a change touches UI + backend integration, validate both

## Local run workflow
- Prefer compose files under `samples/PriorAuth/Compose/`
- Use the smaller compose file for backend-only work
- Use the full compose file when testing the full sample including router/UI/intake
- If only the UI is changing, prefer running the Angular dev server directly
- The PriorAuth seeder is now a local-only workflow; it is no longer run as a compose service
- Seed shared databases locally before starting compose-backed services:
  - `dotnet run --project samples/PriorAuth/Seeder/Kaleido.Samples.PriorAuth.Seeder.csproj -- --domains=ReferenceData,CodeSet,Configuration,ProviderSearch,MemberService`
- Shared SQLite files live under `samples/PriorAuth/data/`
- Compose mounts that host directory into service containers as `/app/data`
- Runtime-created DBs like `eventcollector.db`, `intake.db`, and `intake-process.db` may appear in `samples/PriorAuth/data/` after services start

## Environment assumptions
- Assume `ASPNETCORE_ENVIRONMENT=Development` for local work unless told otherwise
- Use `npm` for the Angular app
- Preserve existing service ports and compose service names unless the task requires changing them

## Local ports
- router: `8080`
- referencedata: `8081`
- codeset: `8082`
- providersearch: `8083`
- memberservice: `8084`
- intake: `8085`
- eventcollector: `8086`
- aspire dashboard: `18888`