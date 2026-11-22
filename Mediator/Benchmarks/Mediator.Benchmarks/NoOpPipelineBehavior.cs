using Mediator.Core.Pipelines;

namespace Mediator.Benchmarks;

public sealed class NoOpPipelineBehavior
    : IPipelineBehavior<TestRequest, int>
{
    public Task<int> HandleAsync(
        TestRequest request,
        CancellationToken cancellationToken,
        Func<Task<int>> next)
    {
        return next();
    }
}