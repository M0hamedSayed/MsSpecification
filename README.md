# MsSpecification

[![NuGet](https://img.shields.io/nuget/v/MsSpecification.Core.svg)](https://www.nuget.org/packages/MsSpecification.Core)
[![NuGet](https://img.shields.io/nuget/v/MsSpecification.EntityFrameworkCore.svg)](https://www.nuget.org/packages/MsSpecification.EntityFrameworkCore)
[![Build](https://github.com/M0hamedSayed/MsSpecification/actions/workflows/build.yml/badge.svg)](https://github.com/M0hamedSayed/MsSpecification/actions/workflows/build.yml)

High-performance Specification Pattern implementation for **.NET 8+** and **Entity Framework Core**.

Build type-safe, reusable, composable query specifications that translate directly into optimized EF Core queries with minimal, well-amortized overhead. See the [Performance](#performance) section for what's cached and where.

> **v2.0 highlights** — Multi-`DbContext` DI routing, `StreamAsync` cancellation, `Select`/`GroupBy` mutex enforcement, bounded filtered-include cache, strong-name signing. See [CHANGELOG.md](CHANGELOG.md) for the full breaking-changes list and migration notes.

## Features

| Feature | Description |
|---|---|
| **Criteria (WHERE)** | Fluent `Where`, `And`, `Or`, `Not` with expression tree parameter rebinding |
| **Includes** | Strongly-typed `Include` / `ThenInclude` chains with array-backed steps |
| **Filtered Includes** | `Include(o => o.Items.Where(i => i.IsActive))` with automatic detection |
| **String Includes** | Escape-hatch `IncludeString("Navigation.Path")` for edge cases |
| **Ordering** | `OrderByAscending`, `OrderByDescending`, `ThenByAscending`, `ThenByDescending` |
| **Pagination** | `Paginate(skip, take)` and `Page(pageNumber, pageSize)` (1-indexed) |
| **Projection** | `MsSpec<T, TResult>` with `Select(x => new Dto { ... })` compiled at DB level |
| **Distinct** | `EnableDistinct()` |
| **GroupBy** | `GroupBy(keySelector, resultSelector)` on `MsSpec<T, TResult>` — typed server-side aggregate |
| **No Tracking** | `EnableNoTracking()` and `EnableNoTrackingWithIdentityResolution()` |
| **Split Query** | `EnableSplitQuery()` to prevent cartesian explosion |
| **Query Tags** | `TagWith("MyQuery")` adds SQL comments for profiling |
| **Ignore Filters** | `EnableIgnoreQueryFilters()` bypasses global query filters |
| **Streaming** | `EnableStreaming()` + `IStreamingSpecificationRepository<T>.StreamAsync()` returns `IAsyncEnumerable<T>` and forwards `CancellationToken`. Automatically applies `AsNoTracking`. Every buffered method (`ListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, `CountAsync<TResult>`) throws `InvalidOperationException` when `AsStreaming` is set. |
| **Raw SQL** | `FromSqlRaw("EXEC sp @p0", param)` for stored procedures, TVFs, and ad-hoc SQL |
| **Spec Composition** | `spec1.And(spec2)` and `spec1.Or(spec2)` extension methods |
| **In-Memory Eval** | `spec.IsSatisfiedBy(entity)` for unit tests and domain validation |
| **Repository** | `ISpecificationRepository<T>` for queries, `IUpdateSpecificationRepository<T>` for bulk updates, `IStreamingSpecificationRepository<T>` for streaming |
| **DI Integration** | `services.AddMsSpecification<MyDbContext>()` one-liner registration |
| **Multi-target** | `net8.0`, `net9.0`, `net10.0` |

## Architecture

```
MsSpecification.Core                   MsSpecification.Infra.EF
(no EF dependency)                     (EF Core integration)
┌──────────────────────────────┐       ┌────────────────────────────────┐
│ ISpecification<T>            │◄──────│ SpecificationEvaluator         │
│ ISpecification<T,R>          │       │ FilteredIncludeApplier         │
│ MsSpec<T>                    │       │ SpecificationRepository<T>     │
│ MsSpec<T,TResult>            │       │ IUpdateSpecificationRepository │
│ ISpecificationRepository<T>  │       │ IMsSpecificationDbContext-     │
│ IStreamingSpecificationRepo  │       │   Resolver  (+ default impl)   │
│ IncludeChainBuilder          │       │ SingleDbContextResolver        │
│ SpecificationExtensions      │       │ QueryableExtensions            │
└──────────────────────────────┘       │ ServiceCollectionExtensions    │
                                       └────────────────────────────────┘
```

The Core package has **zero EF dependency** — you can define specifications in your domain layer without referencing EF Core. The Infra.EF package provides the evaluation engine, the repository implementation, and the resolver infrastructure that lets a single host route specifications across multiple `DbContext` types.

## Installation

```bash
# Core (specification definitions — no EF dependency)
dotnet add package MsSpecification.Core

# EF Core integration (evaluator + repository)
dotnet add package MsSpecification.EntityFrameworkCore
```

## Quick Start

### 1. Define a Specification

```csharp
using MsSpecification.Core.Contracts;

public class ActiveProductsSpec : MsSpec<Product>
{
    public ActiveProductsSpec(int categoryId, int page = 1, int pageSize = 20)
    {
        Where(p => p.IsActive);
        And(p => p.CategoryId == categoryId);
        Include(p => p.Category);
        Include(p => p.Tags);
        OrderByDescending(p => p.CreatedAt);
        Page(page, pageSize);
        EnableNoTracking();
        EnableSplitQuery();
        TagWith("ActiveProducts");
    }
}
```

### 2. Register Services

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddMsSpecification<AppDbContext>();
```

This registers all three repository interfaces:
- `ISpecificationRepository<T>` — queries (List, FirstOrDefault, Count, Any, ExecuteDelete)
- `IUpdateSpecificationRepository<T>` — bulk updates (ExecuteUpdate)
- `IStreamingSpecificationRepository<T>` — streaming (StreamAsync → IAsyncEnumerable)

#### Multiple DbContexts

`AddMsSpecification<TContext>()` is idempotent for the repository registrations and additive
for the `DbContext` factory list. Call it once per context the application uses; the resolver
routes each `ISpecificationRepository<T>` to the context whose model maps `T`.

```csharp
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(appConn));
builder.Services.AddDbContext<LoggingDbContext>(o => o.UseSqlServer(logConn));

builder.Services.AddMsSpecification<AppDbContext>();
builder.Services.AddMsSpecification<LoggingDbContext>();

// Inject ISpecificationRepository<Product> → uses AppDbContext (owns Product)
// Inject ISpecificationRepository<LogEntry> → uses LoggingDbContext (owns LogEntry)
```

If the same entity type is mapped by more than one registered `DbContext`, the resolver throws
`InvalidOperationException` rather than silently picking one — disambiguate by constructing
`SpecificationRepository<T>.ForDbContext(myContext)` directly, or move the entity to a single
context.

#### Direct construction (without DI)

For tests, benchmarks, or one-off scripts you can skip the resolver entirely:

```csharp
using var ctx = new AppDbContext(options);
var repo = SpecificationRepository<Product>.ForDbContext(ctx);
var products = await repo.ListAsync(new ActiveProductsSpec());
```

### 3. Use in Your Code

```csharp
public class ProductService
{
    private readonly ISpecificationRepository<Product> _repo;
    private readonly IStreamingSpecificationRepository<Product> _streamingRepo;

    public ProductService(
        ISpecificationRepository<Product> repo,
        IStreamingSpecificationRepository<Product> streamingRepo)
    {
        _repo = repo;
        _streamingRepo = streamingRepo;
    }

    public Task<List<Product>> GetActiveAsync(int categoryId, int page)
        => _repo.ListAsync(new ActiveProductsSpec(categoryId, page));

    public Task<int> CountActiveAsync(int categoryId)
        => _repo.CountAsync(new ActiveProductsSpec(categoryId));

    public Task<bool> HasActiveAsync(int categoryId)
        => _repo.AnyAsync(new ActiveProductsSpec(categoryId));

    public IAsyncEnumerable<Product> StreamActiveAsync(int categoryId)
        => _streamingRepo.StreamAsync(new ActiveProductsSpec(categoryId));
}
```

## Usage Examples

### Criteria Composition

```csharp
public class ProductSearchSpec : MsSpec<Product>
{
    public ProductSearchSpec(string term, decimal? minPrice, decimal? maxPrice)
    {
        Where(p => p.IsActive);
        And(p => p.Name.Contains(term) || p.Description!.Contains(term));

        if (minPrice.HasValue)
            And(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            And(p => p.Price <= maxPrice.Value);

        OrderByAscending(p => p.Price);
        ThenByAscending(p => p.Name);
        EnableNoTracking();
    }
}
```

### Projection (DTO Mapping at Database Level)

```csharp
public class ProductSummarySpec : MsSpec<Product, ProductSummaryDto>
{
    public ProductSummarySpec()
    {
        Where(p => p.IsActive);
        Select(p => new ProductSummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            CategoryName = p.Category.Name,
            TagCount = p.Tags.Count
        });
        OrderByDescending(p => p.Price);
        EnableNoTracking();
    }
}

// Usage — returns List<ProductSummaryDto> directly from SQL
var summaries = await repo.ListAsync(new ProductSummarySpec());
```

### Deep Include Chains

```csharp
public class OrderWithFullDetailsSpec : MsSpec<Order>
{
    public OrderWithFullDetailsSpec(int orderId)
    {
        Where(o => o.Id == orderId);
        Include(o => o.Items)
            .ThenInclude(i => i.Product)
                .ThenInclude(p => p.Category);
        Include(o => o.Items)
            .ThenInclude(i => i.Product)
                .ThenInclude(p => p.Tags);
        EnableNoTrackingWithIdentityResolution();
        EnableSplitQuery();
    }
}
```

### Filtered Includes

```csharp
public class OrderWithActiveItemsSpec : MsSpec<Order>
{
    public OrderWithActiveItemsSpec()
    {
        // Only load items with quantity > 1
        Include(o => o.Items.Where(i => i.Quantity > 1))
            .ThenInclude(i => i.Product);
        OrderByDescending(o => o.OrderDate);
        EnableNoTracking();
    }
}
```

### Bypassing Global Query Filters

```csharp
public class AllOrdersIncludingDeletedSpec : MsSpec<Order>
{
    public AllOrdersIncludingDeletedSpec(int orderId)
    {
        Where(o => o.Id == orderId);
        EnableIgnoreQueryFilters(); // bypasses soft-delete filter
        EnableNoTracking();
    }
}
```

### Raw SQL / Stored Procedures

```csharp
public class OrdersByStoredProcSpec : MsSpec<Order>
{
    public OrdersByStoredProcSpec(int customerId)
    {
        FromSqlRaw("EXEC GetOrdersByCustomer @p0", customerId);
        OrderByDescending(o => o.OrderDate);
        EnableNoTracking();
    }
}

public class ProductsByFunctionSpec : MsSpec<Product>
{
    public ProductsByFunctionSpec(decimal minPrice)
    {
        FromSqlRaw("SELECT * FROM dbo.GetProductsByMinPrice({0})", minPrice);
        Include(p => p.Category);
        EnableNoTracking();
    }
}

// All standard spec clauses (Where, OrderBy, Pagination, Includes) compose on top of raw SQL
var orders = await repo.ListAsync(new OrdersByStoredProcSpec(42));
```

### Combining Specifications

```csharp
using MsSpecification.Core.Extensions;

var activeSpec = new ActiveProductsSpec();
var premiumSpec = new PremiumProductsSpec();

// Combine with AND — criteria merged, includes merged
var activePremium = activeSpec.And(premiumSpec);
var results = await repo.ListAsync(activePremium);

// Combine with OR
var eitherSpec = activeSpec.Or(premiumSpec);
```

### GroupBy Aggregation

```csharp
// CategorySalesSpec projects to a custom DTO with server-side GROUP BY
public class CategorySalesSpec : MsSpec<OrderItem, CategorySalesDto>
{
    public CategorySalesSpec()
    {
        GroupBy(
            item => item.Product.CategoryId,
            g => new CategorySalesDto
            {
                CategoryId = g.Key,
                TotalRevenue = g.Sum(i => i.Quantity * i.Product.Price),
                OrderCount = g.Count()
            });
        EnableNoTracking();
    }
}

// Returns one CategorySalesDto per category, computed entirely in SQL
var categorySales = await repo.ListAsync(new CategorySalesSpec());
```

### Bulk Operations

```csharp
// Delete all matching entities server-side (no loading into memory)
var spec = new InactiveProductsSpec();
int deleted = await repository.ExecuteDeleteAsync(spec);

// Update all matching entities server-side via IUpdateSpecificationRepository
var updateRepo = scope.ServiceProvider.GetRequiredService<IUpdateSpecificationRepository<Product>>();
int updated = await updateRepo.ExecuteUpdateAsync(
    spec,
    s => s.SetProperty(p => p.Stock, 0)  // EF Core 8/9 syntax
);

// EF Core 10+ uses the new UpdateSettersBuilder API
#if NET10_0_OR_GREATER
int updated = await updateRepo.ExecuteUpdateAsync(
    spec,
    s => s.SetProperty(p => p.Stock, 0)  // UpdateSettersBuilder<T> syntax
);
#endif
```

### Streaming Results

For large result sets, use `IStreamingSpecificationRepository<T>` to stream results as `IAsyncEnumerable<T>` without buffering the entire result set in memory:

```csharp
var streamingRepo = scope.ServiceProvider.GetRequiredService<IStreamingSpecificationRepository<Product>>();

// Stream entities — yields one at a time, no List<T> allocation
await foreach (var product in streamingRepo.StreamAsync(new ActiveProductsSpec()))
{
    // Process each product as it arrives from the database
    await ProcessProductAsync(product);
}

// Stream projected results
await foreach (var dto in streamingRepo.StreamAsync(new ProductSummarySpec()))
{
    Console.WriteLine($"{dto.Name}: {dto.Price:C}");
}
```

### In-Memory Evaluation (Unit Testing)

```csharp
var spec = new ActiveProductsSpec(categoryId: 1);

var product = new Product { IsActive = true, CategoryId = 1 };
Assert.True(spec.IsSatisfiedBy(product));

var inactive = new Product { IsActive = false, CategoryId = 1 };
Assert.False(spec.IsSatisfiedBy(inactive));
```

### Direct IQueryable Composition

```csharp
using MsSpecification.Infra.EF.Extensions;

// Apply spec to any IQueryable
var query = dbContext.Products
    .WithSpecification(new ActiveProductsSpec())
    .Where(p => p.Stock > 100); // additional ad-hoc filter

// Apply only criteria (for Count/Exists without includes/ordering overhead)
var count = await dbContext.Products
    .WithCriteria(new ActiveProductsSpec())
    .CountAsync();
```

The concrete repository also exposes `GetQuery(spec)` as an escape hatch that returns the fully
shaped `IQueryable<T>` for further manual composition:

```csharp
var repo = SpecificationRepository<Product>.ForDbContext(ctx);
IQueryable<Product> q = repo.GetQuery(new ActiveProductsSpec());
var expensive = await q.Where(p => p.Price > 1000).ToListAsync();
```

## API Reference

### MsSpec<T> Protected Builder Methods

| Method | Description |
|---|---|
| `Where(expression)` | Sets the WHERE predicate (replaces previous) |
| `And(expression)` | Combines criteria with AND |
| `Or(expression)` | Combines criteria with OR |
| `Not()` | Negates the current criteria |
| `Include(expression)` | Starts a typed include chain |
| `IncludeString(path)` | Adds a string-based include path |
| `OrderByAscending(key)` | Sets primary ascending sort |
| `OrderByDescending(key)` | Sets primary descending sort |
| `ThenByAscending(key)` | Adds secondary ascending sort |
| `ThenByDescending(key)` | Adds secondary descending sort |
| `Paginate(skip, take)` | Applies skip/take pagination |
| `Page(pageNumber, pageSize)` | Page-based pagination — **1-indexed**. `Page(1, 20)` returns rows 1–20; `Page(2, 20)` returns rows 21–40. Throws if `pageNumber < 1`. |
| `Select(expression)` | Sets T → T projection |
| `EnableNoTracking()` | Disables change tracking |
| `EnableNoTrackingWithIdentityResolution()` | No tracking with identity resolution |
| `EnableSplitQuery()` | Splits into multiple SQL statements |
| `EnableStreaming()` | Marks the spec for streaming. Auto-applies `AsNoTracking`; every buffered method (`ListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, and the obsolete `CountAsync<TResult>`) throws `InvalidOperationException` — use `IStreamingSpecificationRepository.StreamAsync` instead. `StreamAsync` itself forwards the `CancellationToken`. |
| `EnableDistinct()` | Applies DISTINCT |
| `EnableIgnoreQueryFilters()` | Bypasses global query filters |
| `TagWith(tag)` | Adds SQL comment tag for diagnostics |
| `FromSqlRaw(sql, params)` | Sets raw SQL as base query source |

### MsSpec<T, TResult> Additional Methods

| Method | Description |
|---|---|
| `Select(expression)` | Sets T → TResult projection |
| `GroupBy(keySelector, resultSelector)` | Configures a typed server-side GROUP BY. The evaluator runs criteria + includes first, then executes `source.GroupBy(keySelector).Select(resultSelector)` as a single aggregate SQL query. Cannot be combined with `Select`. |

### ISpecificationRepository<T> (Core — Queries)

| Method | Description |
|---|---|
| `ListAsync(spec)` | Returns all matching entities |
| `FirstOrDefaultAsync(spec)` | Returns first match or null |
| `SingleOrDefaultAsync(spec)` | Returns single match or null (throws if >1) |
| `CountAsync(spec)` | Returns count of matching entities |
| `AnyAsync(spec)` | Returns true if any entity matches |
| `ListAsync<TResult>(spec)` | Returns projected results |
| `FirstOrDefaultAsync<TResult>(spec)` | Returns first projected result |
| `SingleOrDefaultAsync<TResult>(spec)` | Returns single projected result |
| ~~`CountAsync<TResult>(spec)`~~ | ⚠️ **Obsolete** — counts projected rows which generates suboptimal SQL. Use `CountAsync(spec)` instead |
| `ExecuteDeleteAsync(spec)` | Bulk-deletes matching entities (server-side) |

### IUpdateSpecificationRepository<T> (Infra.EF — Bulk Updates)

| Method | Description |
|---|---|
| `ExecuteUpdateAsync(spec, setter)` | Bulk-updates matching entities. On .NET 8/9: `Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>>`. On .NET 10+: `Action<UpdateSettersBuilder<T>>` |

### IStreamingSpecificationRepository<T> (Core — Streaming)

| Method | Description |
|---|---|
| `StreamAsync(spec, ct)` | Returns `IAsyncEnumerable<T>` — streams matching entities without buffering |
| `StreamAsync<TResult>(spec, ct)` | Returns `IAsyncEnumerable<TResult>` — streams projected results without buffering |

## Performance

The package is designed for minimal, well-amortized overhead. Per-query work is dominated by
EF Core itself; the specification layer adds only a thin envelope of cached delegates and
struct-backed metadata.

- **Array-backed include chains** — `IncludeChain<T>` stores steps in a single right-sized array (default capacity 4, doubling on growth), so typical 1–3 step chains allocate exactly once with no resizing
- **Generic specialization caches** — `TypeMetadataCache<TFrom, TProperty>` exposes type metadata as `static readonly` fields; the JIT generates one class per type pair, lookups are constant-time field reads
- **ConditionalWeakTable expression caches** — `ExpressionNameCache` extracts property names once per lambda instance, GC-safe
- **String Include fast-path** — non-filtered chains use dot-notation strings (no reflection)
- **Filtered include delegate caching** — typed `Include`/`ThenInclude` chains are compiled once per structural fingerprint and held in a **bounded FIFO cache** (default 1024 entries per entity type) so long-running services never leak compiled delegates
- **Static `MethodInfo` resolution** — `Include` and `ThenInclude` reflection scans run once per process, not once per cache miss
- **Lazy criteria compilation** — `IsSatisfiedBy` compiles the expression tree once per spec via `Lazy<Func<T,bool>>` with `ExecutionAndPublication` thread safety
- **Stackalloc navigation paths** — `BuildNavigationPath` uses `stackalloc` + `Span<char>` for chains up to 256 characters, falling back to heap allocation only for pathological inputs

Run the benchmarks to see actual numbers:

```bash
cd benchmarks/MsSpecification.Benchmarks
dotnet run -c Release
```

## Thread Safety & Spec Reuse

A specification is configured entirely in its constructor and is **effectively immutable**
afterward — none of the builder methods are public, so a fully-constructed spec cannot be
mutated by callers. This makes spec instances **safe to cache and reuse across requests and
threads**:

```csharp
// Safe: a static, shared, parameterless spec reused everywhere
public static readonly ActiveProductsSpec Instance = new();
```

- **`IsSatisfiedBy`** compiles the criteria expression once via `Lazy<Func<T,bool>>` with
  `ExecutionAndPublication` thread-safety, so concurrent first-calls are correct and the
  delegate is built at most once.
- **`SpecificationEvaluator` / `SpecificationRepository<T>`** never mutate the spec — they only
  read its clauses to build an `IQueryable`. Each query runs against the `DbContext` you provide.
- **`DbContext` is not thread-safe** (an EF Core constraint, unrelated to this library). With the
  DI registration the repository is **scoped**, so each request gets its own context — never share
  one repository/context instance across concurrent requests.

## Layering: Which Repository Interface to Depend On

| Interface | Package | Depend on it from |
|---|---|---|
| `ISpecificationRepository<T>` | **Core** (EF-free) | Application / Domain-facing layers — the stable, persistence-agnostic port |
| `IStreamingSpecificationRepository<T>` | **Core** (EF-free) | Application layers that stream large result sets |
| `IUpdateSpecificationRepository<T>` | **Infra.EF** | Infrastructure / composition root only |

`IUpdateSpecificationRepository<T>.ExecuteUpdateAsync` is intentionally **not** in Core: its
signature references EF Core types (`SetPropertyCalls<T>` / `UpdateSettersBuilder<T>`), so
depending on it pulls EF into the consuming layer. If you follow a clean/hexagonal architecture,
keep bulk-update behind an **application-defined port** implemented in Infrastructure, and let
Application code depend only on the EF-free `ISpecificationRepository<T>`.

## Limitations & Gotchas

- **`DISTINCT` on a projected spec applies to the projection.** For `MsSpec<T, TResult>` with a
  `Select`, `EnableDistinct()` produces `SELECT DISTINCT <projected columns>`. For a non-projected
  `MsSpec<T>`, it deduplicates entities.
- **`Skip`/`Take` without ordering is non-deterministic.** Always pair `Paginate`/`Page` with an
  `OrderBy*` for stable pages — EF Core will warn otherwise.
- **`Select` and `GroupBy` are mutually exclusive** on the same `MsSpec<T, TResult>` (throws
  `InvalidOperationException`).
- **Filtered-include delegates are cached with a bound.** The compiled `Include`/`ThenInclude`
  delegates live in a FIFO cache (default 1024 entries per entity type); apps generating an
  unbounded variety of filter shapes evict oldest-first rather than leaking.
- **Raw SQL and bulk `ExecuteDelete`/`ExecuteUpdate` require a relational provider** and EF Core 7+.
- **`AsStreaming` specs reject buffered methods.** Calling `ListAsync`/`FirstOrDefaultAsync`/etc.
  with a streaming spec throws — use `IStreamingSpecificationRepository<T>.StreamAsync` instead.

## Project Structure

```
MsSpecification/
├── src/
│   ├── MsSpecification.Core/                         # Abstractions, base classes, builders (no EF dependency)
│   │   ├── Contracts/                                 # ISpecification, MsSpec, ISpecificationRepository, IStreamingSpecificationRepository
│   │   ├── Builder/                                   # IncludeChain, IncludeChainBuilder, ParameterReplacerVisitor, caches
│   │   ├── Models/                                    # IncludeStep, OrderByClause
│   │   └── Extensions/                                # And/Or combinators
│   └── MsSpecification.Infra.EF/                     # EF Core integration
│       ├── Contracts/                                 # IUpdateSpecificationRepository (EF-specific signature)
│       ├── SpecificationEvaluator.cs                  # Translates spec → IQueryable
│       ├── FilteredIncludeApplier.cs                  # Typed Include/ThenInclude for filtered chains
│       ├── SpecificationRepository.cs                 # Implements all three repository interfaces
│       ├── IMsSpecificationDbContextResolver.cs       # Multi-context routing contract
│       ├── MsSpecificationDbContextResolver.cs       # Default resolver (entity → owning DbContext)
│       ├── SingleDbContextResolver.cs                 # Trivial single-context resolver for direct use
│       └── Extensions/                                # QueryableExtensions, ServiceCollectionExtensions
├── tests/
│   ├── MsSpecification.Core.Tests/                    # Unit tests for Core (xUnit)
│   └── MsSpecification.Infra.EF.Tests/                # SQLite in-memory integration tests for the EF layer
├── samples/
│   ├── MsSpecification.Sample.Api/                    # Working API demonstrating all features
│   └── MsSpecification.Sample.Api.Tests/              # WebApplicationFactory-based integration tests
├── benchmarks/
│   └── MsSpecification.Benchmarks/                    # BenchmarkDotNet: spec vs raw EF comparison
├── docs/
│   ├── archive/                                       # Historical planning notes
│   └── logo.png                                       # Package icon (also at repo root for NuGet)
├── CHANGELOG.md                                       # Release notes, including v1→v2 migration
├── LICENSE                                            # MIT
├── MsSpecification.snk                                # Strong-name key (committed for reproducible signing)
├── logo.png                                           # NuGet package icon
└── README.md
```

## Upgrading from v1.x to v2.0

Most callers need no code changes. The breaking surface is:

| Change | Action required |
|---|---|
| `MsSpec<T>` now requires `T : class` | Specs over value types must be removed — they always failed at runtime when passed to a repository. |
| `MsSpec<T, TResult>.Selector` no longer shadows the base | If you read `spec.Selector` on a non-projected spec, switch to `spec.IdentitySelector`. The builder method `Select(...)` works unchanged. |
| `new SpecificationRepository<T>(dbContext)` removed | Use `SpecificationRepository<T>.ForDbContext(dbContext)` instead. DI-resolved repositories are unaffected. |
| `IncludeChain<T>.Return()` made internal | No production code should call this; if you were, you no longer need to. |
| `Select` + `GroupBy` on the same spec now throws | If a spec previously configured both, one was being silently dropped. Pick one. |

See [CHANGELOG.md](CHANGELOG.md) for the full v2.0.0 entry.

## Requirements

- .NET 8.0 or later
- Entity Framework Core 8.0+ (for the EF integration package)

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for build/test instructions
and coding standards, and [CHANGELOG.md](CHANGELOG.md) for release history.

## License

MIT — see [LICENSE](LICENSE).
