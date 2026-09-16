using Mediator.Core.Requests;

namespace Mediator.Benchmarks;

public sealed record TestRequest() : IRequest<int>;

public sealed class TestRequestHandler : IRequestHandler<TestRequest, int>
{
    public Task<int> HandleAsync(TestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(50);
    }
}