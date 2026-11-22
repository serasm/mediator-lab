using BenchmarkDotNet.Attributes;
using Mediator.Core;
using Mediator.Core.Requests;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 5, iterationCount: 20, launchCount: 10)]
public class ConcurrentSendBenchmarks
{
    [Params(1, 4, 8, 16, 32)]
    public int ConcurrentRequests { get; set; }

    private IMediator _basicMediator = null!;
    private IMediator _handlerCachedMediator = null!;
    private IMediator _pipelineOptimizedMediator = null!;

    private TestRequest _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Analogicznie do SendRequestBenchmarks:
        // konfiguracja ServiceCollection,
        // rejestracja handlera,
        // utworzenie providerów i mediatorów.

        // Wszystkie cache należy rozgrzać.
    }

    [Benchmark(Baseline = true)]
    public Task ConcurrentBasic()
    {
        return Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests)
                .Select(_ => _basicMediator.SendAsync(_request, CancellationToken.None)));
    }

    [Benchmark]
    public Task ConcurrentHandlerCached()
    {
        return Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests)
                .Select(_ => _handlerCachedMediator.SendAsync(_request, CancellationToken.None)));
    }

    [Benchmark]
    public Task ConcurrentPipelineOptimized()
    {
        return Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests)
                .Select(_ => _pipelineOptimizedMediator.SendAsync(_request, CancellationToken.None)));
    }
}