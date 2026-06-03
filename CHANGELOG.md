# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-06-03

This release fixes every Critical and High issue raised in the comprehensive v1.1.0 review,
plus the medium-priority cleanup items. It contains breaking changes around DI, the `Selector`
property, and a few visibility tightenings — all motivated by correctness and clarity.

### Release pipeline & repository hygiene

- **CI build no longer fails on the net8.0/net9.0 legs.** `build.yml` previously forced a single
  `-p:TargetFramework` across the whole solution, which errored (`NETSDK1005`) on the net10-only
  sample projects. The matrix now runs per-OS, builds the full solution once (each project for the
  TFMs it declares), and runs the EF integration suite across net8/net9/net10.
- **Publish pipeline now ships what it builds.** `publish.yml` packed to `bin/Release` but pushed
  from `artifacts/`, so it would have published stale, committed packages. It now packs with
  `--output artifacts`, strips a leading `v` from the release tag for a valid NuGet version, and
  pushes both `.nupkg` and `.snupkg` with `--skip-duplicate`.
- **Added a `.gitignore`** for .NET build output (`bin/`, `obj/`, `.vs/`, `artifacts/`, `*.db*`,
  `BenchmarkDotNet.Artifacts/`, test results) so build artifacts and stale packages are never tracked.
- **`GLOBAL.JSON` renamed to `global.json`** (the SDK ignores the uppercase name on case-sensitive
  file systems such as Linux CI) and given `"rollForward": "latestFeature"`.
- **Benchmark project marked `IsPackable=false`** so `dotnet pack` no longer emits a benchmark package.
- **Repository/project URLs corrected** to `https://github.com/M0hamedSayed/MsSpecification`.

### Behavior & internals

- **`DISTINCT` on a projected spec now applies to the projection.** For `MsSpec<T, TResult>` with a
  `Select`, `EnableDistinct()` produces `SELECT DISTINCT <projected columns>` instead of distinct
  entities followed by projection. Non-projected specs are unchanged.
- **`IncludeChain<T>` no longer uses `ArrayPool`.** The pooled buffer was never returned in
  production (so it provided no benefit and over-allocated to the pool's minimum bucket); it now
  uses a single right-sized array that doubles on growth. Public surface and the `Return()`
  use-after-free guard are unchanged.

### Breaking Changes

- **DI registration is now multi-DbContext aware.** `AddMsSpecification<TContext>()` no longer
  registers a single `DbContext` factory that the latest call overwrites. Instead, it appends
  each `TContext` to an `IEnumerable<DbContext>` that a new `IMsSpecificationDbContextResolver`
  uses to route repositories to the context whose model maps each entity. Single-context apps
  work unchanged. Apps that previously called `AddMsSpecification` more than once for different
  contexts no longer leak the last-write-wins bug.
- **`SpecificationRepository<T>` constructor now takes `IMsSpecificationDbContextResolver`.**
  Direct manual construction is still possible via the new `SpecificationRepository<T>.ForDbContext(DbContext)`
  static factory. Test fixtures and benchmarks have been updated to use it.
- **`MsSpec<T>` now requires `T : class`.** The constraint is propagated through
  `ISpecification<T>`, `ISpecification<T, TResult>`, `MsSpec<T, TResult>`, and the And/Or
  extensions. Specs over value types compiled but failed at runtime when handed to a repository;
  now they fail at compile time.
- **`MsSpec<T, TResult>.Selector` no longer shadows the base property.** The base
  `ISpecification<T>.Selector` (column-pruning `Expression<Func<T,T>>`) was renamed to
  `IdentitySelector`. Projected specs expose `Selector` cleanly without the `new`/explicit
  interface implementation footgun. Both `Select(...)` builder overloads still work the same
  from a caller perspective.
- **`IncludeChain<T>.Return()` is now `internal`.** Callers had no safe way to reason about
  the chain's lifetime; the public method was a use-after-free footgun. The GC handles cleanup.
- **`Select` and `GroupBy` are mutually exclusive at runtime.** Calling both on the same
  `MsSpec<T, TResult>` instance now throws `InvalidOperationException` with a clear message
  instead of silently dropping one of them.

### Fixed

- **`StreamAsync` now respects the `CancellationToken`.** Both overloads were accepting a
  token parameter but discarding it (`AsAsyncEnumerable()` was called with no token).
  Re-implemented as `async IAsyncEnumerable<T>` with `[EnumeratorCancellation]`.
- **`CountAsync<TResult>` now throws when `AsStreaming` is set**, matching every other buffered
  query method on the repository. The obsolete overload no longer silently buffers projection
  results in memory.
- **`FilteredIncludeApplier`'s cache is bounded.** `FilteredIncludeCache<T>` was an unbounded
  `ConcurrentDictionary` — long-running services generating many distinct filter shapes leaked
  compiled delegates. Now backed by a FIFO-evicting cache with a 1024-entry default capacity
  per entity type. Internal `GetCacheCount<T>()` / `SetCacheCapacityForTesting<T>()` hooks
  enable test assertions.
- **Filtered-include fingerprint no longer collides under pathological inputs.** Replaced
  pipe-string separators with control characters (SOH/STX) that cannot appear in CLR type names
  or expression `ToString()` output. Fingerprint also now explicitly encodes `IsCollection`.
- **`Include` and `ThenInclude` reflection results are cached statically.** `BuildRootInclude`
  and `FindThenIncludeMethod` no longer scan `typeof(EntityFrameworkQueryableExtensions).GetMethods()`
  on every cache miss; the open-generic method handles are resolved exactly once per process.
- **`FromSqlRaw` parameters no longer `.ToArray()` on every execution.** The spec stores the
  array directly and the repository pattern-matches to skip the copy.

### Added

- **`LICENSE` file** at repo root (MIT). Previously declared only via `PackageLicenseExpression`.
- **Strong-name signing** is enabled. `MsSpecification.snk` is committed; `Directory.Build.props`
  sets `SignAssembly` + `AssemblyOriginatorKeyFile`. `InternalsVisibleTo` entries now include
  the matching `PublicKey`.
- **`IMsSpecificationDbContextResolver` + `MsSpecificationDbContextResolver` + `SingleDbContextResolver`**
  — the multi-context routing infrastructure described above.
- **`SpecificationRepository<T>.ForDbContext(DbContext)`** — convenience factory for direct,
  non-DI construction (used by tests, benchmarks, and any caller with a single context in hand).
- **Sample API streaming endpoint** (`GET /api/products/stream`) and **bulk-delete endpoint**
  (`DELETE /api/products/inactive`) demonstrating idiomatic use of
  `IStreamingSpecificationRepository<T>` and `ExecuteDeleteAsync`.
- **~25 new tests** covering every fix in this release: `StreamAsync` cancellation parity for
  both arities, `Select`/`GroupBy` mutual exclusion, `CountAsync<TResult>` streaming guard,
  multi-DbContext resolution, fingerprint cache hit/eviction, IgnoreFilters honoured by bulk
  delete/update, concurrent `IsSatisfiedBy` compilation, nullable navigation in
  `IsSatisfiedBy`, `FromSqlRaw` + pagination composition, and SQL-vs-`IsSatisfiedBy` parity
  for the canonical sample specs.
- **CI workflow matrix** over `{ubuntu-latest, windows-latest} × {net8.0, net9.0, net10.0}`,
  now running the full solution (including `MsSpecification.Infra.EF.Tests` which was previously
  excluded) and uploading NuGet artifacts.

### Changed

- **Package author/copyright metadata is concrete.** `Authors` is now `Mohamed Sayed`,
  `Copyright` and `Company` are populated.
- **`Page(int, int)` XML doc spells out 1-indexed semantics** with an explicit example.
- **Duplicate `ParameterReplacerVisitor`** consolidated to
  `src/MsSpecification.Core/Builder/ParameterReplacerVisitor.cs`.
- **Historical planning documents** moved from `plans/` to `docs/archive/`.

## [1.1.0] - 2026-04-24

### Added
- `IStreamingSpecificationRepository<T>` moved to `MsSpecification.Core.Contracts` — the interface has no EF dependency and now lives where it belongs, alongside `ISpecificationRepository<T>`
- `MsSpec<T, TResult>.GroupBy<TKey>(keySelector, resultSelector)` — typed server-side GROUP BY aggregation. Replaced the broken `SetGroupBy(Expression<Func<T,object?>>)` primitive that previously produced an identity query. `GroupByApplier` property added to `ISpecification<T, TResult>` and honored by `SpecificationEvaluator`
- `MsSpecification.Infra.EF.Tests` project — full SQLite in-memory integration test suite covering `SpecificationEvaluator`, projections, `GroupBy`, `SpecificationRepository` (all methods), `IUpdateSpecificationRepository`, `IStreamingSpecificationRepository`, `FilteredIncludeApplier` (filtered chains, deep chains, `IEnumerable<T>` regression, fingerprint cache hits), `QueryableExtensions`, and `ServiceCollectionExtensions`

### Changed
- `AsStreaming` / `EnableStreaming()` now has real semantics: `SpecificationEvaluator` automatically applies `AsNoTracking` when `AsStreaming` is true; `SpecificationRepository<T>` buffered methods (`ListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync` — entity and projection arities) throw `InvalidOperationException` when `AsStreaming` is set, guiding callers to `IStreamingSpecificationRepository.StreamAsync`
- `FilteredIncludeApplier.TryGetEnumerableElement` now handles `IEnumerable<T>` navigation properties declared directly as that interface (not just types that implement it via a secondary interface)
- `FilteredIncludeApplier` cache re-keyed from `IncludeChain<T>` object identity to a structural fingerprint (`FromType.PropertyName:ToType:NavigationPath` per step). Two specification instances describing the same include chain now share the compiled delegate. Cache backed by `ConcurrentDictionary<string, Func<IQueryable<T>, IQueryable<T>>>` via generic holder `FilteredIncludeCache<T>`

### Removed
- `ISpecification<T>.GroupBy` property (`Expression<Func<T, object?>>`) — was applying `GroupBy(...).SelectMany(g => g)` which is an identity operation producing no aggregation
- `MsSpec<T>.SetGroupBy(Expression<Func<T, object?>>)` builder method — replaced by `MsSpec<T, TResult>.GroupBy<TKey>(keySelector, resultSelector)`

## [1.0.0] - 2025-04-23

### Added
- Core specification pattern abstractions (`ISpecification<T>`, `ISpecification<T, TResult>`)
- `MsSpec<T>` and `MsSpec<T, TResult>` base classes with fluent protected builder API
- `ISpecificationRepository<T>` for query execution
- `IUpdateSpecificationRepository<T>` for bulk updates (EF Core 8-10 with TFM-conditional signatures)
- `IStreamingSpecificationRepository<T>` for streaming large result sets via `IAsyncEnumerable<T>`
- `SpecificationEvaluator` for translating specs to `IQueryable<T>`
- `SpecificationRepository<T>` implementing all repository interfaces
- `FilteredIncludeApplier` for typed filtered includes
- `IncludeChain<T>` with ArrayPool-backed step storage
- `IncludeChainBuilder<T, TProperty>` for type-safe ThenInclude chains
- `ExpressionNameCache` with ConditionalWeakTable for property name extraction
- `TypeMetadataCache<TFrom, TProperty>` for zero-cost type metadata via generic specialization
- `SpecificationExtensions.And()` / `.Or()` for combining specifications
- `IsSatisfiedBy(T entity)` for in-memory evaluation with lazy-compiled delegates
- Multi-targeting: net8.0, net9.0, net10.0
- Per-TFM EF Core package references (8.0.x, 9.0.x, 10.0.x)
- `QueryableExtensions.WithSpecification()` / `.WithCriteria()`
- `ServiceCollectionExtensions.AddMsSpecification<TContext>()`
- Strong-name signing with `.snk`
- SourceLink integration for debugging into source
- Symbol package (`.snupkg`) generation
- Comprehensive unit tests for Core project
- Integration tests via sample API
- BenchmarkDotNet benchmarks proving zero-overhead abstraction

### Changed
- `IncludeChain<T>.Return()` now guards against use-after-free with `InvalidOperationException`
- `CombinedSpecification.IsSatisfiedBy()` now uses `Lazy<Func<T, bool>>` for compiled criteria caching
- `BuildNavigationPath` rewritten to use `stackalloc` instead of `steps.ToArray()` allocation
- `CountAsync<TResult>` marked `[Obsolete]` — prefer `CountAsync(ISpecification<T>)` for criteria-only counting

### Removed
- Empty `MsSpecification.Contract` project (Core already serves as the abstraction layer)
