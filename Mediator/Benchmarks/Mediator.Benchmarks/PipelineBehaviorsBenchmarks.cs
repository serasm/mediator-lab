
using BenchmarkDotNet.Attributes;
using Mediator.Core;
using Mediator.Core.Requests;
using Mediator.PipelineOptimization.Mediator;
using Mediator.PipelineOptimization.Pipelines;
using Mediator.Core.Pipelines;
using Mediator.PipelineFactoryOptimization.Mediator;
using Mediator.PipelineOptimization;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 5, iterationCount: 20, launchCount: 5)]
public class PipelineBehaviorsBenchmarks
{
    [Params(0, 1, 3, 5, 10)]
    public int PipelineCount { get; set; }

    [Params(
        ServiceLifetime.Singleton,
        ServiceLifetime.Scoped,
        ServiceLifetime.Transient)]
    public ServiceLifetime ServiceLifetime { get; set; }

    private IMediator _pipelineOptimizationMediator = null!;
    private IMediator _factoryOptimizationMediator = null!;

    private ServiceProvider _rootProvider = null!;
    private IServiceScope? _scope;

    private TestRequest _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        services.AddSingleton<
            IRequestHandler<TestRequest, int>,
            TestRequestHandler>();

        for (var i = 0; i < PipelineCount; i++)
        {
            switch (ServiceLifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton<
                        IPipelineBehavior<TestRequest, int>,
                        NoOpPipelineBehavior>();
                    break;

                case ServiceLifetime.Scoped:
                    services.AddScoped<
                        IPipelineBehavior<TestRequest, int>,
                        NoOpPipelineBehavior>();
                    break;

                case ServiceLifetime.Transient:
                    services.AddTransient<
                        IPipelineBehavior<TestRequest, int>,
                        NoOpPipelineBehavior>();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        _rootProvider = services.BuildServiceProvider();

        IServiceProvider provider = _rootProvider;

        if (ServiceLifetime == ServiceLifetime.Scoped)
        {
            _scope = _rootProvider.CreateScope();
            provider = _scope.ServiceProvider;
        }

        _pipelineOptimizationMediator = new PipelineMediator(
            provider,
            new PipelineOptimization.RequestHandlerInvokerCache(),
            new PipelineOptimization.Pipelines.PipelineFactory());
        
        _factoryOptimizationMediator = new FactoryOptimizationMediator(
            provider,
            new PipelineFactoryOptimization.RequestHandlerInvokerCache(),
            new PipelineFactoryOptimization.Pipelines.PipelineFactory());

        _request = new TestRequest();

        // Warm-up cache.
        _pipelineOptimizationMediator.SendAsync(_request, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        _factoryOptimizationMediator.SendAsync(_request, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    [Benchmark]
    public Task<int> PipelineOptimized()
    {
        return _pipelineOptimizationMediator.SendAsync(
            _request,
            CancellationToken.None);
    }
    
    [Benchmark]
    public Task<int> FactoryOptimized()
    {
        return _factoryOptimizationMediator.SendAsync(
            _request,
            CancellationToken.None);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
        _rootProvider.Dispose();
    }
}
