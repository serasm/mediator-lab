using Mediator.Core.Pipelines;
using Mediator.Core.Requests;
using Mediator.PipelineFactoryOptimization.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.PipelineFactoryOptimization.Tests;

public record PipelineTestRequest(int Value) : IRequest<int>;
public record PipelineOtherRequest(int Value) : IRequest<int>;

public sealed class LoggingBehavior(string name, List<string> log) : IPipelineBehavior<PipelineTestRequest, int>
{
    public async Task<int> HandleAsync(PipelineTestRequest request, CancellationToken cancellationToken, Func<Task<int>> next)
    {
        log.Add($"{name}:before");
        var result = await next();
        log.Add($"{name}:after");
        return result;
    }
}

public sealed class ShortCircuitBehavior : IPipelineBehavior<PipelineTestRequest, int>
{
    public Task<int> HandleAsync(PipelineTestRequest request, CancellationToken cancellationToken, Func<Task<int>> next)
        => Task.FromResult(42); // nie woła next()
}

public sealed class FinallyBehavior(List<string> log) : IPipelineBehavior<PipelineTestRequest, int>
{
    public async Task<int> HandleAsync(PipelineTestRequest request, CancellationToken cancellationToken, Func<Task<int>> next)
    {
        try { return await next(); }
        finally { log.Add("finally"); }
    }
}

public sealed class TokenCapturingBehavior(List<CancellationToken> tokens) : IPipelineBehavior<PipelineTestRequest, int>
{
    public Task<int> HandleAsync(PipelineTestRequest request, CancellationToken cancellationToken, Func<Task<int>> next)
    {
        tokens.Add(cancellationToken);
        return next();
    }
}

public sealed class InstanceIdBehavior(List<Guid> ids) : IPipelineBehavior<PipelineTestRequest, int>
{
    private readonly Guid _id = Guid.NewGuid();

    public Task<int> HandleAsync(PipelineTestRequest request, CancellationToken cancellationToken, Func<Task<int>> next)
    {
        ids.Add(_id);
        return next();
    }
}

public class PipelineFactoryTests
{
    private delegate Task<int> Pipeline(IServiceProvider sp, object handler, IRequest<int> request, CancellationToken ct);
    private static readonly Func<object, IRequest<int>, CancellationToken, Task<int>> EchoInvoker =
        (_, request, _) => Task.FromResult(((PipelineTestRequest)request).Value);

    private static Func<IServiceProvider, object, IRequest<int>, CancellationToken, Task<int>> GetPipeline(
        PipelineFactory factory,
        Func<object, IRequest<int>, CancellationToken, Task<int>>? invoker = null)
        => (Func<IServiceProvider, object, IRequest<int>, CancellationToken, Task<int>>)
            factory.GetOrAdd(typeof(PipelineTestRequest), invoker ?? EchoInvoker);

    private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void GetOrAdd_ShouldReturnSameDelegate_ForSameRequestType()
    {
        var factory = new PipelineFactory();

        var first = factory.GetOrAdd(typeof(PipelineTestRequest), EchoInvoker);
        var second = factory.GetOrAdd(typeof(PipelineTestRequest), EchoInvoker);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrAdd_ShouldReturnDifferentDelegates_ForDifferentRequestTypes()
    {
        var factory = new PipelineFactory();

        var first = factory.GetOrAdd(typeof(PipelineTestRequest), EchoInvoker);
        var second = factory.GetOrAdd(typeof(PipelineOtherRequest), EchoInvoker);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetOrAdd_ShouldReturnDelegateOfExpectedType()
    {
        var factory = new PipelineFactory();

        var result = factory.GetOrAdd(typeof(PipelineTestRequest), EchoInvoker);

        Assert.IsType<Func<IServiceProvider, object, IRequest<int>, CancellationToken, Task<int>>>(result);
    }

    [Fact]
    public async Task GetOrAdd_ShouldReturnSameDelegate_WhenCalledConcurrently()
    {
        var factory = new PipelineFactory();

        var delegates = await Task.WhenAll(
            Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() => factory.GetOrAdd(typeof(PipelineTestRequest), EchoInvoker))));

        Assert.All(delegates, d => Assert.Same(delegates[0], d));
    }

    [Fact]
    public async Task Pipeline_ShouldCallHandler_WhenNoBehaviorsRegistered()
    {
        var pipeline = GetPipeline(new PipelineFactory());
        await using var provider = BuildProvider();

        var result = await pipeline(provider, new object(), new PipelineTestRequest(7), CancellationToken.None);

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task Pipeline_ShouldPassHandlerAndRequest_ToHandlerInvoker()
    {
        object? receivedHandler = null;
        IRequest<int>? receivedRequest = null;
        var pipeline = GetPipeline(new PipelineFactory(), (handler, request, _) =>
        {
            receivedHandler = handler;
            receivedRequest = request;
            return Task.FromResult(1);
        });
        await using var provider = BuildProvider();
        var handlerObject = new object();
        var requestObject = new PipelineTestRequest(5);

        await pipeline(provider, handlerObject, requestObject, CancellationToken.None);

        Assert.Same(handlerObject, receivedHandler);
        Assert.Same(requestObject, receivedRequest);
    }

    [Fact]
    public async Task Pipeline_ShouldRunBehaviorsInRegistrationOrder()
    {
        var log = new List<string>();
        var pipeline = GetPipeline(new PipelineFactory(), (_, _, _) =>
        {
            log.Add("handler");
            return Task.FromResult(1);
        });
        await using var provider = BuildProvider(s =>
        {
            s.AddTransient<IPipelineBehavior<PipelineTestRequest, int>>(_ => new LoggingBehavior("first", log));
            s.AddTransient<IPipelineBehavior<PipelineTestRequest, int>>(_ => new LoggingBehavior("second", log));
        });

        await pipeline(provider, new object(), new PipelineTestRequest(1), CancellationToken.None);

        Assert.Equal(
            new[] { "first:before", "second:before", "handler", "second:after", "first:after" },
            log);
    }

    [Fact]
    public async Task Pipeline_ShouldAllowBehaviorToShortCircuit()
    {
        var handlerCalled = false;
        var pipeline = GetPipeline(new PipelineFactory(), (_, _, _) =>
        {
            handlerCalled = true;
            return Task.FromResult(1);
        });
        await using var provider = BuildProvider(s =>
            s.AddTransient<IPipelineBehavior<PipelineTestRequest, int>, ShortCircuitBehavior>());

        var result = await pipeline(provider, new object(), new PipelineTestRequest(1), CancellationToken.None);

        Assert.Equal(42, result);
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task Pipeline_ShouldPropagateHandlerException_ThroughBehaviors()
    {
        var log = new List<string>();
        var pipeline = GetPipeline(new PipelineFactory(),
            (_, _, _) => Task.FromException<int>(new InvalidOperationException("boom")));
        await using var provider = BuildProvider(s =>
            s.AddTransient<IPipelineBehavior<PipelineTestRequest, int>>(_ => new FinallyBehavior(log)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline(provider, new object(), new PipelineTestRequest(1), CancellationToken.None));

        Assert.Equal("boom", ex.Message);
        Assert.Contains("finally", log);
    }

    [Fact]
    public async Task Pipeline_ShouldPassCancellationToken_ToBehaviorAndHandler()
    {
        using var cts = new CancellationTokenSource();
        var tokens = new List<CancellationToken>();
        CancellationToken handlerToken = default;
        var pipeline = GetPipeline(new PipelineFactory(), (_, _, ct) =>
        {
            handlerToken = ct;
            return Task.FromResult(1);
        });
        await using var provider = BuildProvider(s =>
            s.AddTransient<IPipelineBehavior<PipelineTestRequest, int>>(_ => new TokenCapturingBehavior(tokens)));

        await pipeline(provider, new object(), new PipelineTestRequest(1), cts.Token);

        Assert.Equal(cts.Token, Assert.Single(tokens));
        Assert.Equal(cts.Token, handlerToken);
    }

    [Fact]
    public async Task Pipeline_ShouldResolveBehaviorsFromProviderPassedToEachCall()
    {
        var log = new List<string>();
        var pipeline = GetPipeline(new PipelineFactory());
        await using var providerWithBehavior = BuildProvider(s =>
            s.AddTransient<IPipelineBehavior<PipelineTestRequest, int>>(_ => new LoggingBehavior("A", log)));
        await using var emptyProvider = BuildProvider();

        await pipeline(providerWithBehavior, new object(), new PipelineTestRequest(1), CancellationToken.None);
        Assert.Equal(new[] { "A:before", "A:after" }, log);

        log.Clear();
        await pipeline(emptyProvider, new object(), new PipelineTestRequest(1), CancellationToken.None);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Pipeline_ShouldResolveNewTransientBehaviorInstance_OnEveryCall()
    {
        var ids = new List<Guid>();
        var pipeline = GetPipeline(new PipelineFactory());
        await using var provider = BuildProvider(s =>
            s.AddTransient<IPipelineBehavior<PipelineTestRequest, int>>(_ => new InstanceIdBehavior(ids)));

        await pipeline(provider, new object(), new PipelineTestRequest(1), CancellationToken.None);
        await pipeline(provider, new object(), new PipelineTestRequest(1), CancellationToken.None);

        Assert.Equal(2, ids.Count);
        Assert.NotEqual(ids[0], ids[1]);
    }
}