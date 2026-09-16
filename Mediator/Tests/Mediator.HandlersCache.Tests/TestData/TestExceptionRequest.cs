using Mediator.Core.Requests;

namespace Mediator.HandlersCache.Tests.TestData;

public record TestExceptionRequest(int value) : IRequest<int>;

public class TestExceptionRequestHandler : IRequestHandler<TestExceptionRequest, int>
{
    public Task<int> HandleAsync(TestExceptionRequest query, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Request test exception");
    }
}