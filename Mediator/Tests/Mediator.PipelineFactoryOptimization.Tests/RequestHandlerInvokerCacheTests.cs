namespace Mediator.PipelineFactoryOptimization.Tests;

public class RequestHandlerInvokerCacheTests
{
    [Fact]
    public void GetOrAdd_WhenCalledTwiceForSameRequestType_ReturnsCachedInvoker()
    {
        // Arrange
        var cache = new RequestHandlerInvokerCache();

        var firstInvoker = CreateInvoker();
        var secondInvoker = CreateInvoker();

        // Act
        var firstResult = cache.GetOrAdd(
            typeof(TestRequest),
            _ => (typeof(TestHandler), firstInvoker));

        var secondResult = cache.GetOrAdd(
            typeof(TestRequest),
            _ => (typeof(TestHandler), secondInvoker));

        // Assert
        Assert.Same(firstInvoker, firstResult.Invoker);
        Assert.Same(firstResult.Invoker, secondResult.Invoker);
        Assert.Equal(typeof(TestHandler), secondResult.RequestHandlerType);
    }

    [Fact]
    public void GetOrAdd_WhenCalledForDifferentRequestTypes_CreatesSeparateEntries()
    {
        // Arrange
        var cache = new RequestHandlerInvokerCache();

        var firstInvoker = CreateInvoker();
        var secondInvoker = CreateInvoker();

        // Act
        var firstResult = cache.GetOrAdd(
            typeof(TestRequest),
            _ => (typeof(TestHandler), firstInvoker));

        var secondResult = cache.GetOrAdd(
            typeof(AnotherTestRequest),
            _ => (typeof(AnotherTestHandler), secondInvoker));

        // Assert
        Assert.Same(firstInvoker, firstResult.Invoker);
        Assert.Same(secondInvoker, secondResult.Invoker);

        Assert.NotSame(firstResult.Invoker, secondResult.Invoker);
        Assert.Equal(typeof(TestHandler), firstResult.RequestHandlerType);
        Assert.Equal(typeof(AnotherTestHandler), secondResult.RequestHandlerType);
    }

    [Fact]
    public void GetOrAdd_WhenCalledConcurrently_ReturnsTheSameCachedInvoker()
    {
        // Arrange
        var cache = new RequestHandlerInvokerCache();
        var invokers = new Delegate[32];
        var factoryCallCount = 0;

        // Act
        Parallel.For(0, invokers.Length, index =>
        {
            var result = cache.GetOrAdd(
                typeof(TestRequest),
                _ =>
                {
                    Interlocked.Increment(ref factoryCallCount);

                    return (typeof(TestHandler), CreateInvoker());
                });

            invokers[index] = result.Invoker;
        });

        // Assert
        Assert.All(
            invokers,
            invoker => Assert.Same(invokers[0], invoker));

        Assert.InRange(factoryCallCount, 1, invokers.Length);
    }

    private static Delegate CreateInvoker()
    {
        var result = 42;

        return new Func<object, object, CancellationToken, Task<int>>(
            (_, _, _) => Task.FromResult(result));
    }

    private sealed class TestRequest {}
    private sealed class AnotherTestRequest {}

    private sealed class TestHandler {}
    private sealed class AnotherTestHandler {}
}