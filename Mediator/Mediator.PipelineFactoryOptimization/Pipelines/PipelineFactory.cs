using System.Collections.Concurrent;
using System.Reflection;
using Mediator.Core.Pipelines;
using Mediator.Core.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.PipelineFactoryOptimization.Pipelines;

public class PipelineFactory : IPipelineFactory
{
    private readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), Delegate> _cache = new();

    private static readonly MethodInfo BuildMethod =
        typeof(PipelineFactory).GetMethod(nameof(Build), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Method 'Build' not found");

    public Delegate GetOrAdd<TResponse>(
        Type requestType,
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker)
        => _cache.GetOrAdd(
            (requestType, typeof(TResponse)),
            static (key, invoker) => BuildClosedPipeline(key.RequestType, invoker),
            handlerInvoker);

    private static Delegate BuildClosedPipeline<TResponse>(
        Type requestType,
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker)
        => (Delegate)BuildMethod
            .MakeGenericMethod(requestType, typeof(TResponse))
            .Invoke(null, new object[] { handlerInvoker })!;

    private static Func<IServiceProvider, object, IRequest<TResponse>, CancellationToken, Task<TResponse>>
        Build<TRequest, TResponse>(
            Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker)
        where TRequest : IRequest<TResponse>
    {
        return (serviceProvider, handler, request, cancellationToken) =>
        {
            var services = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var behaviors = services as IPipelineBehavior<TRequest, TResponse>[] ?? services.ToArray();

            if (behaviors.Length == 0)
                return handlerInvoker(handler, request, cancellationToken);

            return new Step<TRequest, TResponse>(
                behaviors, 0, handlerInvoker, handler, (TRequest)request, cancellationToken).Invoke();
        };
    }

    private sealed class Step<TRequest, TResponse>(
        IPipelineBehavior<TRequest, TResponse>[] behaviors,
        int index,
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker,
        object handler,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> Invoke()
        {
            if (index == behaviors.Length)
                return handlerInvoker(handler, request, cancellationToken);

            var next = new Step<TRequest, TResponse>(
                behaviors, index + 1, handlerInvoker, handler, request, cancellationToken);

            return behaviors[index].HandleAsync(request, cancellationToken, next.Invoke);
        }
    }
}