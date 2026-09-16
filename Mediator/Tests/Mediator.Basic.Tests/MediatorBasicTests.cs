using Mediator.Basic.Tests.TestData;
using Mediator.Core.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Basic.Tests;

public class MediatorBasicTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenServiceProviderIsNull()
    {
        // Act
        var act = () => new MediatorBasic(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SendAsync_ShouldReturnHandlerResponse()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTransient<
            IRequestHandler<TestRequest, string>,
            TestRequestHandler>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = new MediatorBasic(serviceProvider);

        var request = new TestRequest();

        // Act
        var result = await mediator.SendAsync(request);

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task SendAsync_ShouldPassRequestToHandler()
    {
        // Arrange
        var handler = new CapturingTestRequestHandler();

        var services = new ServiceCollection();

        services.AddSingleton<
            IRequestHandler<TestRequest, string>>(handler);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = new MediatorBasic(serviceProvider);

        var request = new TestRequest
        {
            Value = 42
        };

        // Act
        await mediator.SendAsync(request);

        // Assert
        Assert.Same(request, handler.ReceivedRequest);
    }

    [Fact]
    public async Task SendAsync_ShouldPassCancellationTokenToHandler()
    {
        // Arrange
        var handler = new CapturingTestRequestHandler();

        var services = new ServiceCollection();

        services.AddSingleton<
            IRequestHandler<TestRequest, string>>(handler);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = new MediatorBasic(serviceProvider);

        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await mediator.SendAsync(
            new TestRequest(),
            cancellationTokenSource.Token);

        // Assert
        Assert.Equal(
            cancellationTokenSource.Token,
            handler.ReceivedCancellationToken);
    }

    [Fact]
    public async Task SendAsync_ShouldThrow_WhenHandlerIsNotRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = new MediatorBasic(serviceProvider);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync(new TestRequest()));

        // Assert
        Assert.Equal(
            "No handler registered for type IRequestHandler`2",
            exception.Message);
    }

    [Fact]
    public async Task SendAsync_ShouldPropagateHandlerException()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTransient<
            IRequestHandler<TestRequest, string>,
            ThrowingTestRequestHandler>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = new MediatorBasic(serviceProvider);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync(new TestRequest()));

        // Assert
        Assert.Equal("Handler failed.", exception.Message);
    }

    [Fact]
    public async Task SendAsync_ShouldResolveCorrectHandlerForRequestType()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTransient<
            IRequestHandler<TestRequest, string>,
            TestRequestHandler>();

        services.AddTransient<
            IRequestHandler<AnotherTestRequest, string>,
            AnotherTestRequestHandler>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = new MediatorBasic(serviceProvider);

        // Act
        var firstResult = await mediator.SendAsync(
            new TestRequest());

        var secondResult = await mediator.SendAsync(
            new AnotherTestRequest());

        // Assert
        Assert.Equal("Hello", firstResult);
        Assert.Equal("World", secondResult);
    }
}