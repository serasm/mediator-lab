using Mediator.Core.Requests;

namespace Mediator.Basic.Tests.TestData;

internal sealed class TestRequest : IRequest<string>
{
    public int Value { get; init; }
}

internal sealed class TestRequestHandler
    : IRequestHandler<TestRequest, string>
{
    public Task<string> HandleAsync(
        TestRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult("Hello");
    }
}

internal sealed class CapturingTestRequestHandler
    : IRequestHandler<TestRequest, string>
{
    public TestRequest? ReceivedRequest { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<string> HandleAsync(
        TestRequest request,
        CancellationToken cancellationToken)
    {
        ReceivedRequest = request;
        ReceivedCancellationToken = cancellationToken;

        return Task.FromResult("OK");
    }
}

internal sealed class ThrowingTestRequestHandler
    : IRequestHandler<TestRequest, string>
{
    public Task<string> HandleAsync(
        TestRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Handler failed.");
    }
}