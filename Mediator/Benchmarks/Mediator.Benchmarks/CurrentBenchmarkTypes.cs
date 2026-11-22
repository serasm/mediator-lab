namespace Mediator.Benchmarks;

public sealed record CurrentTestRequest : global::Mediator.Core.Requests.IRequest<int>;

// ciało dopasuj do tego, co robi TestRequestHandler (to samo obciążenie)
public sealed class CurrentTestRequestHandler
    : global::Mediator.Core.Requests.IRequestHandler<CurrentTestRequest, int>
{
    public Task<int> HandleAsync(CurrentTestRequest request, CancellationToken cancellationToken)
        => Task.FromResult(50);
}

public sealed class CurrentNoOpBehavior
    : global::Mediator.Core.Pipelines.IPipelineBehavior<CurrentTestRequest, int>
{
    public Task<int> HandleAsync(
        CurrentTestRequest request, CancellationToken cancellationToken, Func<Task<int>> next)
        => next();
}