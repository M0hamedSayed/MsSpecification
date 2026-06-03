using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using MsSpecification.Benchmarks;

// Default CsProj toolchain recursively scans the project tree for the host .csproj. On some volumes (e.g.
// non-NTFS / network) leftover obj folders can become inaccessible and throw UnauthorizedAccessException.
// In-process emit avoids that discovery/build path while still running real benchmark workloads.
var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));

BenchmarkRunner.Run<QueryBenchmarks>(config);
