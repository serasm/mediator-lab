using Mediator.Core.Requests;

namespace Mediator.HandlersCache.Tests.TestData;

public record TestRequest(int value) : IRequest<int> {}

public class TestRequestHandler : IRequestHandler<TestRequest, int>
{
    public CancellationToken CancellationToken { get; private set; }
    
    public Task<int> HandleAsync(TestRequest query, CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        return Task.FromResult(query.value * 2);
    }
}