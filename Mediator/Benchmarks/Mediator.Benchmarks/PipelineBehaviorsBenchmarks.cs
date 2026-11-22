using BenchmarkDotNet.Attributes;
using Mediator.Core;
using Mediator.Core.Pipelines;
using Mediator.Core.Requests;
using Microsoft.Extensions.DependencyInjection;
using PipelineMediator = Mediator.PipelineOptimization.Mediator.PipelineMediator;
using PipelineInvokerCache = Mediator.PipelineOptimization.RequestHandlerInvokerCache;
using PipelineFactory = Mediator.PipelineOptimization.Pipelines.PipelineFactory;
using FactoryMediator = Mediator.PipelineFactoryOptimization.Mediator.FactoryOptimizationMediator;
using FactoryInvokerCache = Mediator.PipelineFactoryOptimization.RequestHandlerInvokerCache;
using FactoryPipelineFactory = Mediator.PipelineFactoryOptimization.Pipelines.PipelineFactory;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 5, iterationCount: 25, launchCount: 8)]
public class PipelineBehaviorsBenchmarks
{
    [Params(0, 1, 3, 10)]
    public int PipelineCount { get; set; }

    private ServiceProvider _provider = null!;
    private IMediator _pipelineOptimization = null!;
    private IMediator _pipelineFactoryOptimization = null!;
    private readonly TestRequest _request = new();

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        // handler Singleton: izolujemy koszt pipeline'ów od kosztu tworzenia handlera
        services.AddSingleton<IRequestHandler<TestRequest, int>, TestRequestHandler>();

        // Transient: typowy lifetime behaviorów, najgorszy przypadek pod względem alokacji
        for (var i = 0; i < PipelineCount; i++)
            services.AddTransient<IPipelineBehavior<TestRequest, int>, NoOpPipelineBehavior>();

        _provider = services.BuildServiceProvider();

        _pipelineOptimization = new PipelineMediator(
            _provider, new PipelineInvokerCache(), new PipelineFactory());
        _pipelineFactoryOptimization = new FactoryMediator(
            _provider, new FactoryInvokerCache(), new FactoryPipelineFactory());

        _pipelineOptimization.SendAsync(_request, CancellationToken.None).GetAwaiter().GetResult();
        _pipelineFactoryOptimization.SendAsync(_request, CancellationToken.None).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    [Benchmark(Baseline = true)]
    public Task<int> PipelineOptimization() => _pipelineOptimization.SendAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> PipelineFactoryOptimization() => _pipelineFactoryOptimization.SendAsync(_request, CancellationToken.None);
}