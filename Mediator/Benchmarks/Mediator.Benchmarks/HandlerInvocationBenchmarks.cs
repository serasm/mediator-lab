using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Mediator.Core.Requests;

namespace Mediator.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 10, iterationCount: 30, launchCount: 5)]
public class HandlerInvocationBenchmarks
{
    private readonly TestRequest _request = new();
    private readonly TestRequestHandler _handler = new();

    private Func<object, IRequest<int>, CancellationToken, Task<int>>
        _expressionInvoker = null!;

    private MethodInfo _handleMethod = null!;
    private MethodInvoker _methodInvoker = null!;

    [GlobalSetup]
    public void Setup()
    {
        var handlerType =
            typeof(IRequestHandler<TestRequest, int>);

        _handleMethod = handlerType.GetMethod(
            nameof(IRequestHandler<TestRequest, int>.HandleAsync))
            ?? throw new InvalidOperationException(
                "HandleAsync method not found.");

        _methodInvoker = MethodInvoker.Create(_handleMethod);

        var handlerParam =
            Expression.Parameter(typeof(object), "handler");

        var requestParam =
            Expression.Parameter(typeof(IRequest<int>), "request");

        var cancellationTokenParam =
            Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var body = Expression.Call(
            Expression.Convert(handlerParam, handlerType),
            _handleMethod,
            Expression.Convert(requestParam, typeof(TestRequest)),
            cancellationTokenParam);

        _expressionInvoker =
            Expression.Lambda<
                Func<object, IRequest<int>, CancellationToken, Task<int>>>(
                body,
                handlerParam,
                requestParam,
                cancellationTokenParam)
            .Compile();
    }
    
    private readonly IRequestHandler<TestRequest, int> _handlerViaInterface = new TestRequestHandler();

    [Benchmark(Baseline = true)]
    public Task<int> DirectViaInterface() => _handlerViaInterface.HandleAsync(_request, CancellationToken.None);

    [Benchmark]
    public Task<int> Direct()
    {
        return _handler.HandleAsync(
            _request,
            CancellationToken.None);
    }

    [Benchmark]
    public Task<int> Reflection()
    {
        return (Task<int>)_handleMethod.Invoke(
            _handler,
            new object?[] { _request, CancellationToken.None })!;
    }

    [Benchmark]
    public Task<int> InvokeWithExpression()
    {
        return _expressionInvoker(
            _handler,
            _request,
            CancellationToken.None);
    }

    [Benchmark]
    public Task<int> InvokeWithMethodInvoker()
    {
        return (Task<int>)_methodInvoker.Invoke(_handler, _request, CancellationToken.None)!;
    }
}
