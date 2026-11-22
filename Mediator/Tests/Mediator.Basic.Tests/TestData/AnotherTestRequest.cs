using Mediator.Core.Requests;

namespace Mediator.Basic.Tests.TestData;

internal sealed class AnotherTestRequest : IRequest<string>
{
}

internal sealed class AnotherTestRequestHandler
    : IRequestHandler<AnotherTestRequest, string>
{
    public Task<string> HandleAsync(
        AnotherTestRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult("World");
    }
}