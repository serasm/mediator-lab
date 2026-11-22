using BenchmarkDotNet.Attributes;
using Mediator.Basic;
using Mediator.Core;
using Mediator.Core.Requests;
using Mediator.HandlersCache;
using Mediator.PipelineOptimization;
using Mediator.PipelineOptimization.Mediator;
using Mediator.PipelineOptimization.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 5, iterationCount: 20, launchCount: 10)]
public class ColdStartBenchmarks
{
    private ServiceProvider _provider = null!;
    private TestRequest _request = null!;

    private IMediator _basicMediator = null!;
    private IMediator _handlerCachedMediator = null!;
    private IMediator _pipelineOptimizedMediator = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        services.AddSingleton<
            IRequestHandler<TestRequest, int>,
            TestRequestHandler>();

        _provider = services.BuildServiceProvider();
        _request = new TestRequest();
    }

    [IterationSetup]
    public void SetupIteration()
    {
        _basicMediator = new MediatorBasic(_provider);
        _handlerCachedMediator = new MediatorHandlersCache(_provider);

        _pipelineOptimizedMediator =
            new PipelineMediator(
                _provider,
                new RequestHandlerInvokerCache(),
                new PipelineFactory());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _provider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<int> Basic()
    {
        return _basicMediator.SendAsync(_request, CancellationToken.None);
    }

    [Benchmark]
    public Task<int> HandlerCachedCold()
    {
        return _handlerCachedMediator.SendAsync(_request, CancellationToken.None);
    }

    [Benchmark]
    public Task<int> PipelineOptimizedCold()
    {
        return _pipelineOptimizedMediator.SendAsync(_request, CancellationToken.None);
    }
}