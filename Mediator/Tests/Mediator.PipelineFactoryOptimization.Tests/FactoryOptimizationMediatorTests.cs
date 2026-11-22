using Mediator.Core.Requests;
using Mediator.PipelineFactoryOptimization.Mediator;
using Mediator.PipelineFactoryOptimization.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.PipelineFactoryOptimization.Tests;

public class FactoryOptimizationMediatorTests
{
    [Fact]
    public void Constructor_WhenServiceProviderIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FactoryOptimizationMediator(
                null!,
                new RequestHandlerInvokerCache(),
                new PipelineFactory()));
    }

    [Fact]
    public void Constructor_WhenRequestHandlerInvokerCacheIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FactoryOptimizationMediator(
                new ServiceCollection().BuildServiceProvider(),
                null!,
                new PipelineFactory()));
    }

    [Fact]
    public void Constructor_WhenPipelineFactoryIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FactoryOptimizationMediator(
                new ServiceCollection().BuildServiceProvider(),
                new RequestHandlerInvokerCache(),
                null!));
    }

    [Fact]
    public async Task SendAsync_WhenHandlerIsRegistered_ReturnsExpectedResponse()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTransient<
            IRequestHandler<TestRequest, int>,
            TestRequestHandler>();

        using var serviceProvider = services.BuildServiceProvider();

        var mediator = CreateMediator(serviceProvider);
        var request = new TestRequest(21);

        // Act
        var result = await mediator.SendAsync(request);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task SendAsync_WhenHandlerIsNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        using var serviceProvider =
            new ServiceCollection().BuildServiceProvider();

        var mediator = CreateMediator(serviceProvider);

        var request = new TestRequest(21);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync(request));

        // Assert
        Assert.Contains(
            "No handler registered",
            exception.Message);
    }

    [Fact]
    public async Task SendAsync_WhenCalled_PassesCancellationTokenToHandler()
    {
        // Arrange
        var handler = new TestRequestHandler();

        var services = new ServiceCollection();

        services.AddSingleton<
            IRequestHandler<TestRequest, int>>(handler);

        using var serviceProvider = services.BuildServiceProvider();

        var mediator = CreateMediator(serviceProvider);

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await mediator.SendAsync(
            new TestRequest(21),
            cancellationToken);

        // Assert
        Assert.Equal(cancellationToken, handler.ReceivedCancellationToken);
    }

    [Fact]
    public async Task SendAsync_WhenCalledForDifferentRequestTypes_UsesCorrectHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTransient<
            IRequestHandler<TestRequest, int>,
            TestRequestHandler>();

        services.AddTransient<
            IRequestHandler<AnotherTestRequest, string>,
            AnotherTestRequestHandler>();

        using var serviceProvider = services.BuildServiceProvider();

        var mediator = CreateMediator(serviceProvider);

        var firstRequest = new TestRequest(21);
        var secondRequest = new AnotherTestRequest("Hello");

        // Act
        var firstResult = await mediator.SendAsync(firstRequest);
        var secondResult = await mediator.SendAsync(secondRequest);

        // Assert
        Assert.Equal(42, firstResult);
        Assert.Equal("Hello World", secondResult);
    }

    private static FactoryOptimizationMediator CreateMediator(
        IServiceProvider serviceProvider)
    {
        return new FactoryOptimizationMediator(
            serviceProvider,
            new RequestHandlerInvokerCache(),
            new PipelineFactory());
    }

    public sealed record TestRequest(int Value) : IRequest<int>;

    public sealed record AnotherTestRequest(string Value) : IRequest<string>;

    public sealed class TestRequestHandler
        : IRequestHandler<TestRequest, int>
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<int> HandleAsync(
            TestRequest request,
            CancellationToken cancellationToken)
        {
            ReceivedCancellationToken = cancellationToken;

            return Task.FromResult(request.Value * 2);
        }
    }

    public sealed class AnotherTestRequestHandler
        : IRequestHandler<AnotherTestRequest, string>
    {
        public Task<string> HandleAsync(
            AnotherTestRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult($"{request.Value} World");
        }
    }
}
