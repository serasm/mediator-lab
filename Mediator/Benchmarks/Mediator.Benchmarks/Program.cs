using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Mediator.Benchmarks;

class Program
{
    private static readonly Type[] BenchmarkTypes =
    {
        typeof(SendRequestBenchmarks),
        typeof(PipelineBehaviorsBenchmarks),
        typeof(ColdStartBenchmarks),
        typeof(HandlerInvocationBenchmarks)
    };

    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            BenchmarkSwitcher.FromTypes(BenchmarkTypes).Run(args);
            return;
        }

        var runDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "benchmark-results",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        foreach (var type in BenchmarkTypes)
        {
            var config = DefaultConfig.Instance
                .WithArtifactsPath(Path.Combine(runDirectory, type.Name))
                .AddExporter(JsonExporter.Full);

            var summary = BenchmarkRunner.Run(type, config);

            SaveTextReport(summary, Path.Combine(runDirectory, $"{type.Name}.txt"));
        }

        Console.WriteLine($"Results are saved in: {runDirectory}");
    }

    private static void SaveTextReport(Summary summary, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var writer = new StreamWriter(path);
        var logger = new StreamLogger(writer);

        MarkdownExporter.GitHub.ExportToLog(summary, logger);
    }
}