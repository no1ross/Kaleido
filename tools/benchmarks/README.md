# Kaleido benchmarks

On-demand measurements with [BenchmarkDotNet](https://benchmarkdotnet.org/), run when a performance
question needs a number - not part of the test suite or CI (CI only builds the project).

```
dotnet run -c Release --project tools/benchmarks/Kaleido.Benchmarks -- --filter '*'
```

Benchmarks run in process (a short-run job) so BenchmarkDotNet does not generate a project that
inherits this repository's build settings. Record a result where the decision it supports is
documented, with the machine it ran on.

## Recorded results

### Counting a full page (`QueryCountBenchmarks`, #326)

A full page is when the query engine counts the results: it reads the page (50 rows), then the
total through `IQueryContextExecutor<TView>`. EF Core with SQLite (in memory), every row matching;
Kaleido's default executor against an executor using EF Core's `CountAsync`/`ToListAsync`.

| Rows | Default executor | Provider executor (`SELECT COUNT`) |
|---|---|---|
| 1,000 | 0.66 ms, 395 KB | 0.13 ms, 84 KB |
| 10,000 | 5.9 ms, 3.2 MB | 0.37 ms, 84 KB |
| 100,000 | 55.9 ms, 31.3 MB | 3.0 ms, 84 KB |

BenchmarkDotNet 0.15.8, .NET 8.0.31, Windows 11, Intel Core Ultra 7 265H.

Conclusion: the default counts an async query by reading every matching row, so its cost grows with
the matches; a provider executor's does not. Core stays provider-neutral; a host on an async provider
(EF Core, MongoDB, ...) registers that provider's executor -
[`docs/PATTERNS.md`](../../docs/PATTERNS.md#queryable-sources-views-and-query-contexts).
