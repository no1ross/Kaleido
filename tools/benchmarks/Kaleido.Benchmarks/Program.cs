using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// In process: BenchmarkDotNet's default toolchain generates and builds a separate project, which
// would inherit this repository's Directory.Build.props (analyzers, warnings as errors).
var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance))
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

BenchmarkSwitcher.FromAssembly(typeof(Kaleido.Benchmarks.QueryCountBenchmarks).Assembly).Run(args, config);
