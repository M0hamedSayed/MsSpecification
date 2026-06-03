# Contributing to MsSpecification

Thank you for your interest in contributing! This document outlines the process and standards for contributing to the MsSpecification project.

## Quick Start

1. **Fork** the repository
2. **Clone** your fork locally
3. Create a **feature branch** from `main`
4. Make your changes
5. **Build and test** — ensure all tests pass
6. **Submit a Pull Request**

## Build & Test

```bash
# Build all projects
dotnet build -c Release

# Run unit tests
dotnet test tests/MsSpecification.Core.Tests -c Release

# Run integration tests
dotnet test samples/MsSpecification.Sample.Api.Tests -c Release

# Run benchmarks (optional, slow)
cd benchmarks/MsSpecification.Benchmarks && dotnet run -c Release
```

## Coding Standards

- **C# latest** language version with nullable reference types enabled
- All public members must have **XML documentation comments**
- `TreatWarningsAsErrors` is enabled — zero warnings allowed
- Use `var` only when the type is apparent; otherwise, use explicit types
- Prefer expression-bodied members for simple properties and one-liner methods
- All `internal` types that need test access should be covered by `InternalsVisibleTo`

## Pull Request Process

1. **One concern per PR** — keep changes focused
2. **Add tests** — every new feature or bug fix must have corresponding tests
3. **Update documentation** — if you change the public API, update README.md
4. **No breaking changes** on the `main` branch without prior discussion
5. Ensure CI passes (build + test on all TFMs)

## Adding New Features

### Specification Features

New specification features should:

1. Add a property to `ISpecification<T>` (if query-shaping)
2. Add a protected builder method to `MsSpec<T>`
3. Handle the new clause in `SpecificationEvaluator`
4. Add unit tests in `MsSpecification.Core.Tests`
5. Add integration test coverage in the sample API tests

### Repository Features

New repository methods that depend on EF Core types should:

1. Go in a **new interface** in `MsSpecification.Infra.EF/Contracts/` (not in Core)
2. Be implemented by `SpecificationRepository<T>`
3. Be registered in `ServiceCollectionExtensions.AddMsSpecification<TContext>()`

This keeps the Core package EF-free by design.

## Reporting Issues

- Use GitHub Issues
- Include repro steps, expected behavior, and actual behavior
- Specify the .NET and EF Core versions you're using

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
