using BenchmarkDotNet.Attributes;
using Mediator.Basic;
using Mediator.Core;
using Mediator.Core.Requests;
using Mediator.HandlersCache;
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
    private IServiceScope _scope = null!;

    private TestRequest _testRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        switch (ServiceLifetime)
        {
            case ServiceLifetime.Scoped:
                services.AddScoped<IRequestHandler<TestRequest, int>, TestRequestHandler>();
                break;
            
            case ServiceLifetime.Singleton:
                services.AddSingleton<IRequestHandler<TestRequest, int>, TestRequestHandler>();
                break;

            case ServiceLifetime.Transient:
                services.AddTransient<IRequestHandler<TestRequest, int>, TestRequestHandler>();
                break;
        }
        
        IServiceProvider provider = services.BuildServiceProvider();

        if (ServiceLifetime == ServiceLifetime.Scoped)
        {
            _scope = provider.CreateScope();
            provider = _scope.ServiceProvider;
        }

        _basicMediator = new MediatorBasic(provider);
        _handlerCachedMediator = new MediatorHandlersCache(provider);
        
        _testRequest = new TestRequest();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<int> Basic()
    {
        return _basicMediator.SendAsync(_testRequest, CancellationToken.None);
    }
    
    [Benchmark]
    public Task<int> Cached()
    {
        return _handlerCachedMediator.SendAsync(_testRequest, CancellationToken.None);
    }
}