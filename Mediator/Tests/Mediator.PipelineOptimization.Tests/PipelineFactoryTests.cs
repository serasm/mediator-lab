using Mediator.Core.Pipelines;
using Mediator.Core.Requests;
using Mediator.PipelineOptimization.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.PipelineOptimization.Tests;

public class PipelineFactoryTests
{
    [Fact]
    public async Task GetOrAdd_WhenNoBehaviorsAreRegistered_InvokesHandlerDirectly()
    {
        // Arrange
        var factory = new PipelineFactory();
        using var provider = new ServiceCollection().BuildServiceProvider();

        var handlerCallCount = 0;
        var request = new TestRequest();

        Func<object, IRequest<int>, CancellationToken, Task<int>> handlerInvoker =
            (_, _, _) =>
            {
                handlerCallCount++;
                return Task.FromResult(42);
            };

        var pipeline = CreatePipeline(factory, handlerInvoker);

        // Act
        var result = await pipeline(
            provider,
            new object(),
            request,
            CancellationToken.None);

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(1, handlerCallCount);
    }

    [Fact]
    public async Task GetOrAdd_WhenOneBehaviorIsRegistered_InvokesBehaviorAndHandler()
    {
        // Arrange
        var executionOrder = new List<string>();

        var services = new ServiceCollection();

        services.AddSingleton<IPipelineBehavior<TestRequest, int>>(
            new RecordingBehavior("Behavior", executionOrder));

        using var provider = services.BuildServiceProvider();

        var request = new TestRequest();

        Func<object, IRequest<int>, CancellationToken, Task<int>> handlerInvoker =
            (_, _, _) =>
            {
                executionOrder.Add("Handler");
                return Task.FromResult(42);
            };

        var factory = new PipelineFactory();
        var pipeline = CreatePipeline(factory, handlerInvoker);

        // Act
        var result = await pipeline(
            provider,
            new object(),
            request,
            CancellationToken.None);

        // Assert
        Assert.Equal(42, result);

        Assert.Equal(
            new[]
            {
                "Behavior:Before",
                "Handler",
                "Behavior:After"
            },
            executionOrder);
    }

    [Fact]
    public async Task GetOrAdd_WhenMultipleBehaviorsAreRegistered_ExecutesThemInRegistrationOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var services = new ServiceCollection();

        services.AddSingleton<IPipelineBehavior<TestRequest, int>>(
            new RecordingBehavior("First", executionOrder));

        services.AddSingleton<IPipelineBehavior<TestRequest, int>>(
            new RecordingBehavior("Second", executionOrder));

        using var provider = services.BuildServiceProvider();

        Func<object, IRequest<int>, CancellationToken, Task<int>> handlerInvoker =
            (_, _, _) =>
            {
                executionOrder.Add("Handler");
                return Task.FromResult(42);
            };

        var factory = new PipelineFactory();
        var pipeline = CreatePipeline(factory, handlerInvoker);

        // Act
        var result = await pipeline(
            provider,
            new object(),
            new TestRequest(),
            CancellationToken.None);

        // Assert
        Assert.Equal(42, result);

        Assert.Equal(
            new[]
            {
                "First:Before",
                "Second:Before",
                "Handler",
                "Second:After",
                "First:After"
            },
            executionOrder);
    }

    [Fact]
    public void GetOrAdd_WhenCalledTwiceForSameTypes_ReturnsCachedPipeline()
    {
        // Arrange
        var factory = new PipelineFactory();

        Func<object, IRequest<int>, CancellationToken, Task<int>> firstInvoker =
            (_, _, _) => Task.FromResult(1);

        Func<object, IRequest<int>, CancellationToken, Task<int>> secondInvoker =
            (_, _, _) => Task.FromResult(2);

        // Act
        var firstPipeline = factory.GetOrAdd<int>(
            typeof(TestRequest),
            firstInvoker);

        var secondPipeline = factory.GetOrAdd<int>(
            typeof(TestRequest),
            secondInvoker);

        // Assert
        Assert.Same(firstPipeline, secondPipeline);
    }

    [Fact]
    public async Task GetOrAdd_PassesCancellationTokenToBehaviorAndHandler()
    {
        // Arrange
        var receivedToken = CancellationToken.None;
        using var cts = new CancellationTokenSource();

        var services = new ServiceCollection();

        services.AddSingleton<IPipelineBehavior<TestRequest, int>>(
            new CancellationTokenRecordingBehavior(token =>
                receivedToken = token));

        using var provider = services.BuildServiceProvider();

        var handlerReceivedToken = CancellationToken.None;

        Func<object, IRequest<int>, CancellationToken, Task<int>> handlerInvoker =
            (_, _, token) =>
            {
                handlerReceivedToken = token;
                return Task.FromResult(42);
            };

        var factory = new PipelineFactory();
        var pipeline = CreatePipeline(factory, handlerInvoker);

        // Act
        await pipeline(
            provider,
            new object(),
            new TestRequest(),
            cts.Token);

        // Assert
        Assert.Equal(cts.Token, receivedToken);
        Assert.Equal(cts.Token, handlerReceivedToken);
    }

    private static Func<
        IServiceProvider,
        object,
        IRequest<int>,
        CancellationToken,
        Task<int>> CreatePipeline(
        PipelineFactory factory,
        Func<object, IRequest<int>, CancellationToken, Task<int>> handlerInvoker)
    {
        return (Func<
            IServiceProvider,
            object,
            IRequest<int>,
            CancellationToken,
            Task<int>>)factory.GetOrAdd(
                typeof(TestRequest),
                handlerInvoker);
    }

    private sealed class TestRequest : IRequest<int> {}

    private sealed class RecordingBehavior(
        string name,
        List<string> executionOrder)
        : IPipelineBehavior<TestRequest, int>
    {
        public async Task<int> HandleAsync(
            TestRequest request,
            CancellationToken cancellationToken,
            Func<Task<int>> next)
        {
            executionOrder.Add($"{name}:Before");

            var result = await next();

            executionOrder.Add($"{name}:After");

            return result;
        }
    }

    private sealed class CancellationTokenRecordingBehavior(
        Action<CancellationToken> recordToken)
        : IPipelineBehavior<TestRequest, int>
    {
        public Task<int> HandleAsync(
            TestRequest request,
            CancellationToken cancellationToken,
            Func<Task<int>> next)
        {
            recordToken(cancellationToken);

            return next();
        }
    }
}