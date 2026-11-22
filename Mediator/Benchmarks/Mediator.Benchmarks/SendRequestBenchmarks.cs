
using BenchmarkDotNet.Attributes;
using Mediator.Basic;
using Mediator.Core;
using Mediator.Core.Requests;
using Mediator.HandlersCache;
using Mediator.PipelineFactoryOptimization.Mediator;
using Mediator.PipelineOptimization;
using Mediator.PipelineOptimization.Mediator;
using Mediator.PipelineOptimization.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 5, iterationCount: 20, launchCount: 10)]
public class SendRequestBenchmarks
{
    [Params(
        ServiceLifetime.Singleton,
        ServiceLifetime.Scoped,
        ServiceLifetime.Transient)]
    public ServiceLifetime ServiceLifetime { get; set; }

    private IMediator _basicMediator = null!;
    private IMediator _handlerCachedMediator = null!;
    private IMediator _pipelineOptimizedMediator = null!;
    private IMediator _factoryOptimizedMediator = null!;

    private ServiceProvider _rootProvider = null!;
    private IServiceScope? _scope;

    private TestRequest _testRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        switch (ServiceLifetime)
        {
            case ServiceLifetime.Scoped:
                services.AddScoped<
                    IRequestHandler<TestRequest, int>,
                    TestRequestHandler>();
                break;

            case ServiceLifetime.Singleton:
                services.AddSingleton<
                    IRequestHandler<TestRequest, int>,
                    TestRequestHandler>();
                break;

            case ServiceLifetime.Transient:
                services.AddTransient<
                    IRequestHandler<TestRequest, int>,
                    TestRequestHandler>();
                break;
        }

        _rootProvider = services.BuildServiceProvider();

        IServiceProvider provider = _rootProvider;

        if (ServiceLifetime == ServiceLifetime.Scoped)
        {
            _scope = _rootProvider.CreateScope();
            provider = _scope.ServiceProvider;
        }

        _basicMediator = new MediatorBasic(provider);
        _handlerCachedMediator = new MediatorHandlersCache(provider);

        _pipelineOptimizedMediator = new PipelineMediator(
            provider,
            new PipelineOptimization.RequestHandlerInvokerCache(),
            new PipelineOptimization.Pipelines.PipelineFactory());
        
        _factoryOptimizedMediator = new FactoryOptimizationMediator(
            provider,
            new PipelineFactoryOptimization.RequestHandlerInvokerCache(),
            new PipelineFactoryOptimization.Pipelines.PipelineFactory());

        _testRequest = new TestRequest();

        // Warm-up cache.
        _handlerCachedMediator.SendAsync(_testRequest, CancellationToken.None).GetAwaiter().GetResult();
        _pipelineOptimizedMediator.SendAsync(_testRequest, CancellationToken.None).GetAwaiter().GetResult();
        _factoryOptimizedMediator.SendAsync(_testRequest, CancellationToken.None).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
        _rootProvider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<int> Basic()
    {
        return _basicMediator.SendAsync(
            _testRequest,
            CancellationToken.None);
    }

    [Benchmark]
    public Task<int> HandlerCached()
    {
        return _handlerCachedMediator.SendAsync(
            _testRequest,
            CancellationToken.None);
    }

    [Benchmark]
    public Task<int> PipelineOptimized()
    {
        return _pipelineOptimizedMediator.SendAsync(
            _testRequest,
            CancellationToken.None);
    }
    
    [Benchmark]
    public Task<int> FactoryOptimized()
    {
        return _pipelineOptimizedMediator.SendAsync(
            _testRequest,
            CancellationToken.None);
    }
}
