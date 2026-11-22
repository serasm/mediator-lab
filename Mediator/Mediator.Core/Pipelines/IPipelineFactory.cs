using Mediator.Core.Requests;

namespace Mediator.Core.Pipelines;

public interface IPipelineFactory
{
    public Delegate GetOrAdd<TResponse>(
        Type requestType,
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker);
}