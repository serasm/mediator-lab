using BenchmarkDotNet.Attributes;
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
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 8, iterationCount: 25, launchCount: 5)]
public class SendRequestBenchmarks
{
    [Params(ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient)]
    public ServiceLifetime HandlerLifetime { get; set; }

    private ServiceProvider _rootProvider = null!;
    private IServiceScope? _scope;

    private IMediator _basic = null!;
    private IMediator _handlerCache = null!;
    private IMediator _pipelineOptimization = null!;
    private IMediator _pipelineFactoryOptimization = null!;

    private IRequestHandler<TestRequest, int> _handlerViaInterface = null!;
    private readonly TestRequest _request = new();

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.Add(new ServiceDescriptor(
            typeof(IRequestHandler<TestRequest, int>), typeof(TestRequestHandler), HandlerLifetime));

        _rootProvider = services.BuildServiceProvider();

        IServiceProvider provider = _rootProvider;
        if (HandlerLifetime == ServiceLifetime.Scoped)
        {
            _scope = _rootProvider.CreateScope();
            provider = _scope.ServiceProvider;
        }

        _basic = new BasicMediator(provider);
        _handlerCache = new HandlersCacheMediator(provider);
        _pipelineOptimization = new PipelineMediator(
            provider, new PipelineInvokerCache(), new PipelineFactory());
        _pipelineFactoryOptimization = new FactoryMediator(
            provider, new FactoryInvokerCache(), new FactoryPipelineFactory());

        _handlerViaInterface = new TestRequestHandler();

        // rozgrzewka cache'y
        _handlerCache.SendAsync(_request, CancellationToken.None).GetAwaiter().GetResult();
        _pipelineOptimization.SendAsync(_request, CancellationToken.None).GetAwaiter().GetResult();
        _pipelineFactoryOptimization.SendAsync(_request, CancellationToken.None).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
        _rootProvider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<int> Basic() => _basic.SendAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> HandlersCache() => _handlerCache.SendAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> PipelineOptimization() => _pipelineOptimization.SendAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> PipelineFactoryOptimization() => _pipelineFactoryOptimization.SendAsync(_request, CancellationToken.None);

    // --- punkty odniesienia ---
    [Benchmark]
    public Task<int> DirectViaInterface() => _handlerViaInterface.HandleAsync(_request, CancellationToken.None);

    [Benchmark]
    public async Task<int> ScopeOnly() // sam koszt DI i scope'u, bez mediatora
    {
        using var scope = _rootProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<TestRequest, int>>();
        return await handler.HandleAsync(_request, CancellationToken.None);
    }
}