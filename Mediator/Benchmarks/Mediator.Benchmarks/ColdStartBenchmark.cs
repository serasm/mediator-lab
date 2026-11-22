using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Mediator.Core;
using Mediator.Core.Requests;
using Microsoft.Extensions.DependencyInjection;
using BasicMediator = Mediator.Basic.MediatorBasic;
using HandlersCacheMediator = Mediator.HandlersCache.MediatorHandlersCache;
using PipelineMediator = Mediator.PipelineOptimization.Mediator.PipelineMediator;
using PipelineInvokerCache = Mediator.PipelineOptimization.RequestHandlerInvokerCache;
using PipelineFactory = Mediator.PipelineOptimization.Pipelines.PipelineFactory;
using FactoryMediator = Mediator.PipelineFactoryOptimization.Mediator.FactoryOptimizationMediator;
using FactoryInvokerCache = Mediator.PipelineFactoryOptimization.RequestHandlerInvokerCache;
using FactoryPipelineFactory = Mediator.PipelineFactoryOptimization.Pipelines.PipelineFactory;

namespace Mediator.Benchmarks;

// każdy benchmark to osobny proces z jednym wywołaniem
[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 10, warmupCount: 0, iterationCount: 1)]
public class ColdStartBenchmarks
{
    private ServiceProvider _provider = null!;
    private IMediator _basic = null!;
    private IMediator _handlerCache = null!;
    private IMediator _pipelineOptimization = null!;
    private IMediator _pipelineFactoryOptimization = null!;
    private readonly TestRequest _request = new();

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<TestRequest, int>, TestRequestHandler>();
        _provider = services.BuildServiceProvider();

        _basic = new BasicMediator(_provider);
        _handlerCache = new HandlersCacheMediator(_provider);
        _pipelineOptimization = new PipelineMediator(
            _provider, new PipelineInvokerCache(), new PipelineFactory());
        _pipelineFactoryOptimization = new FactoryMediator(
            _provider, new FactoryInvokerCache(), new FactoryPipelineFactory());
        // celowo bez żadnego SendAsync: pierwsze wywołanie ma być w benchmarku
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    [Benchmark(Baseline = true)]
    public Task<int> Basic() => _basic.SendAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> HandlersCache() => _handlerCache.SendAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> PipelineOptimization() => _pipelineOptimization.SendAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> PipelineFactoryOptimization() => _pipelineFactoryOptimization.SendAsync(_request, CancellationToken.None);
}