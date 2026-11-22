using BenchmarkDotNet.Running;

namespace Mediator.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<SendRequestBenchmarks>();
    }
}