# MsSpecification — Production Readiness Plan (Reviewed)

This document **reviews and verifies** [`production-readiness-plan.md`](production-readiness-plan.md) against the current repository. It **does not replace** that file; use this as the architecture-correct supplement, especially for **Phase 1.1**.

**Review date:** 2026-04-23  
**Scope:** Read-only verification of source; no implementation changes were made in the library as part of this review.

---

## 1. Executive summary

The original plan is **largely accurate** relative to the code: the critical gaps it calls out (missing `ExecuteUpdateAsync` on the public contract, streaming flag unused, `IncludeChain.Return` safety, `CombinedSpecification` compilation behavior, empty Contract scaffold) are **confirmed** below.

The main **architectural gap** in **§1.1** is not the EF-free Core boundary—it is **where consumers should depend on types**. Putting only `IUpdateSpecificationRepository<T>` in **Infra.EF** fixes the compiler and keeps Core clean, but it **does not** by itself let you use a repository abstraction **correctly from a typical Application (or Domain-facing) layer** without taking a **dependency on EF**, because that interface’s method signatures **reference EF Core types** (`SetPropertyCalls<T>`, `UpdateSettersBuilder<T>`).

**Recommendation:** Treat **`ISpecificationRepository<T>`** (Core) as the **only** repository interface that **Application / Domain-oriented projects** reference from this library. Treat **bulk update** as either **(A)** an **application-defined port** implemented in Infrastructure, **(B)** an optional **Infra-only** interface for the composition root, and/or **(C)** a future **technology-neutral** bulk-update API in Core if the library maintainers want to invest in translation from neutral setters to EF.

---

## 2. Verification matrix (original plan vs code)

| Item | Original claim | Verification | Notes |
|------|----------------|--------------|--------|
| **1.1** | `ExecuteUpdateAsync` exists on `SpecificationRepository<T>` but not on `ISpecificationRepository<T>` | **Confirmed** | `ISpecificationRepository` ends at `ExecuteDeleteAsync` ([`ISpecificationRepository.cs`](../src/MsSpecification.Core/Contracts/ISpecificationRepository.cs)); implementation adds `ExecuteUpdateAsync` with `#if NET10_0_OR_GREATER` ([`SpecificationRepository.cs`](../src/MsSpecification.Infra.EF/SpecificationRepository.cs)). |
| **1.1** | TFM-specific signatures (`Expression<...SetPropertyCalls...>` vs `Action<UpdateSettersBuilder<>>`) | **Confirmed** | Matches implementation lines 99–116 in `SpecificationRepository.cs`. |
| **1.2** | `AsStreaming` / `EnableStreaming()` not applied in evaluator/repository | **Confirmed** | `SpecificationEvaluator.GetQuery` never reads `AsStreaming` ([`SpecificationEvaluator.cs`](../src/MsSpecification.Infra.EF/SpecificationEvaluator.cs)); repository uses `ToListAsync` / `FirstOrDefaultAsync` etc., not `AsAsyncEnumerable`. |
| **1.3** | `MsSpecification.Contract` empty / not in solution | **Mostly confirmed** | [`MsSpecification.slnx`](../MsSpecification.slnx) lists only Core, Infra.EF, samples, benchmarks. Under `src/MsSpecification.Contract/` there are empty `Enums/`, `Interfaces/`, `Models/`, plus `bin/`/`obj/`; **no `.csproj` present** in the workspace listing (orphan build artifacts may remain). Safe to remove the folder per original plan; double-check no external consumers. |
| **2.2** | `CombinedSpecification.IsSatisfiedBy` compiles on every call | **Confirmed** | [`SpecificationExtensions.cs`](../src/MsSpecification.Core/Extensions/SpecificationExtensions.cs) line 114: `Criteria.Compile()(entity)`. |
| **2.3** | `IncludeChain.Return()` allows use-after-free | **Confirmed** | [`IncludeChain.cs`](../src/MsSpecification.Core/Builder/IncludeChain.cs): `Return()` returns the pooled array but does not invalidate `Steps` / `Count`; subsequent access uses a returned buffer. |
| **3.4** | `CountAsync<TResult>` semantics | **Confirmed** | Present on interface and implementation with `evaluateCriteriaOnly: true` then `CountAsync` on projected query—matches plan’s concern. |

**Dependency registration (context for 1.1):** [`ServiceCollectionExtensions.AddMsSpecification<TContext>`](../src/MsSpecification.Infra.EF/Extensions/ServiceCollectionExtensions.cs) registers only `ISpecificationRepository<>` → `SpecificationRepository<>` today; no separate registration for update-specific contract.

---

## 3. Phase 1.1 — Better solution for “use `ISpecificationRepository` in Domain/Application correctly”

### 3.1 What the original plan solves

- **Problem:** Consumers holding `ISpecificationRepository<T>` cannot call `ExecuteUpdateAsync` even though the concrete `SpecificationRepository<T>` implements it.
- **Original fix:** Add `IUpdateSpecificationRepository<T>` in **Infra.EF** with TFM-conditional signatures; implement on `SpecificationRepository<T>`; register in DI; keep Core EF-free.

This is **correct** for **interface segregation** and **keeping Core free of EF**.

### 3.2 What it does *not* solve (your requirement)

If **Application** (or a “domain-facing” layer) injects `IUpdateSpecificationRepository<T>`:

- That project must **reference `MsSpecification.Infra.EF`** (or a shared project that exposes EF types).
- **Bulk update** lambdas use **EF Core surface types**, so **EF leaks** into the layer that should stay persistence-agnostic.

That conflicts with the usual rule: **Application depends on abstractions; Infrastructure depends on EF.**

### 3.3 Recommended layering (consumer solution architecture)

**Rule:** In your solution, **Application (and pure Domain)** should depend on **`MsSpecification.Core` only** for specification-based persistence ports. **Do not** reference **`MsSpecification.Infra.EF`** from Application **except** at the composition root (often the host/API project), if at all.

| Concern | Contract to use in Application | Where EF-specific APIs live |
|--------|--------------------------------|-----------------------------|
| Queries, lists, counts, `Any`, projections, `ExecuteDeleteAsync` | `ISpecificationRepository<T>` (Core) | `SpecificationRepository<T>` (Infra) |
| Bulk update with `SetProperty` / `UpdateSettersBuilder` | **Not** suitable as an Application-facing port | Infra-only, or hidden inside an app-defined adapter |

**Pattern A — Application-defined ports (recommended default)**

- Define **narrow interfaces** in **Application** (or a small `*.Application.Contracts` project), e.g. `IProductCommandStore`, `IOrderArchiveService`, etc.
- Methods encode **use cases**, not EF: e.g. `Task<int> SoftDeleteArchivedAsync(CancellationToken ct)` or `Task<int> SetStatusForSelectionAsync(OrderStatus status, ISpecification<Order> filter, CancellationToken ct)` where the **last parameter is still Core’s** `ISpecification<T>`.
- **Implement** those interfaces in **Infrastructure** with a class that depends on `SpecificationRepository<T>` (or `DbContext`) and calls **`ExecuteUpdateAsync` internally** using EF types **inside Infra only**.

**Pattern B — Library `IUpdateSpecificationRepository<T>` as Infra-only**

- Still add `IUpdateSpecificationRepository<T>` per the original plan **for**:
  - Composition-root wiring,
  - Integration tests,
  - Advanced apps where an **Infrastructure service** (not Application) performs bulk updates.
- **Do not** inject this interface into Application services if you want a clean hexagonal boundary.

**Pattern C — Optional future: neutral bulk-update API in Core (library evolution)**

- If the package authors want **first-class** bulk update **without** EF types at the call site, Core could define something like a **port** with **BCL-only** types, e.g. a sequence of property assignments expressed via **`Expression<Func<T, TProperty>>`** plus values, or a small immutable **update descriptor** model, with **Infra** translating to `ExecuteUpdateAsync`.
- **Trade-off:** Higher design and maintenance cost; must guarantee translation parity with EF’s supported `SetProperty` shapes and TFM differences.

### 3.4 Revised implementation checklist for 1.1 (supersedes the “usage story” only)

Keep the **file-level** changes from the original §1.1 (`IUpdateSpecificationRepository` in Infra, registration, README) **if** you want parity between concrete capabilities and an explicit interface.

**Add** the following **documentation and sample guidance** (when you implement):

1. State explicitly in README: **`ISpecificationRepository<T>` is the stable, EF-free port for Application layers.**
2. Describe **`IUpdateSpecificationRepository<T>`** as **Infrastructure-oriented** (or “advanced / host-level”), not as the default injection target for application use cases.
3. Recommend **Pattern A** for teams that need bulk update from application code without referencing Infra.EF.

### 3.5 DI shape (illustrative, not prescriptive code)

- Continue registering `ISpecificationRepository<>` for all standard operations.
- Optionally register `IUpdateSpecificationRepository<>` **only** where EF-specific bulk update is acceptable.
- Application services: constructor depends on **`ISpecificationRepository<T>`** + **application-owned command ports**; **not** on `IUpdateSpecificationRepository<T>` unless you consciously accept the EF coupling.

---

## 4. Other phases — brief re-confirmation

- **1.2 Streaming:** Plan remains valid; evaluator still ignores `AsStreaming`.
- **1.3 Contract folder:** Removing the empty scaffold is still reasonable; verify no `.csproj` consumers in other repos.
- **2.x / 3.x:** Findings in §2 support proceeding as in the original document unless you choose to defer strong-name signing (team policy).

---

## 5. Suggested implementation order (delta from original)

Where the original plan lists “Add `IUpdateSpecificationRepository`” as a single step, split intent:

1. Implement **`IUpdateSpecificationRepository<T>`** + DI + README **technical** section (original step).
2. Add **architecture** section to README (or a short `docs/layering.md` if you add docs later) describing **Pattern A** and **Application-only dependency on Core**.
3. Optionally add a **sample** `IProductCommandStore` in the sample API’s Infrastructure folder to demonstrate **bulk update behind an app port** (sample-only; optional).

---

## 6. References (key files reviewed)

- [`ISpecificationRepository.cs`](../src/MsSpecification.Core/Contracts/ISpecificationRepository.cs)
- [`SpecificationRepository.cs`](../src/MsSpecification.Infra.EF/SpecificationRepository.cs)
- [`ServiceCollectionExtensions.cs`](../src/MsSpecification.Infra.EF/Extensions/ServiceCollectionExtensions.cs)
- [`SpecificationEvaluator.cs`](../src/MsSpecification.Infra.EF/SpecificationEvaluator.cs)
- [`ISpecification.cs`](../src/MsSpecification.Core/Contracts/ISpecification.cs) (`AsStreaming`)
- [`SpecificationExtensions.cs`](../src/MsSpecification.Core/Extensions/SpecificationExtensions.cs) (`CombinedSpecification`)
- [`IncludeChain.cs`](../src/MsSpecification.Core/Builder/IncludeChain.cs)
- [`MsSpecification.slnx`](../MsSpecification.slnx)

---

*End of reviewed plan. The original [`production-readiness-plan.md`](production-readiness-plan.md) remains the master checklist for file-level edits; use this document to align **§1.1** with **clean architecture** and **Application-layer** usage of `ISpecificationRepository`.*
