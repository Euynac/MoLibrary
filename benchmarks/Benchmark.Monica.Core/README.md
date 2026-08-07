# Benchmark.Monica.Core

This project measures module composition, one-pass type discovery, immutable diagnostics projection/cache behavior,
and discovery-reference collectibility. Run benchmarks from a Release build and filter the scenario being investigated:

```powershell
dotnet run -c Release --project D:\Repositories\WorkTree1\MoLibrary\benchmarks\Benchmark.Monica.Core\Benchmark.Monica.Core.csproj -- --filter *TypeDiscovery*
```

The benchmark groups cover:

- 100, 1,000, and 10,000 discovered types against 1, 5, 15, and 50 distinct queries.
- 10, 50, and 200 composed modules with 0%, 50%, and 90% empty discovery declarations.
- Fresh diagnostics projection and terminal cache lookup at 25, 100, and 500 modules.
- Fresh diagnostics projection and terminal cache lookup at exactly 200, 2,000, and 20,000 normalized trace spans.
- Release/collect behavior for a precompiled `RunAndCollect` discovery assembly. Dynamic emission and compilation run in
  iteration setup; the reported value is the number of forced collection attempts. A surviving assembly throws and marks
  the benchmark invocation as failed instead of returning an easy-to-miss `false` result.

The composition matrix scans only the benchmark assembly and replaces bootstrap console logging with a null logger during
the real module lifecycle. This keeps the type corpus stable and prevents console I/O from dominating measured composition.

The cardinalities intentionally include expensive stress cases. Do not run the complete matrix as part of normal CI;
use the build and test suite for correctness gates, then run selected benchmarks when comparing performance changes.
