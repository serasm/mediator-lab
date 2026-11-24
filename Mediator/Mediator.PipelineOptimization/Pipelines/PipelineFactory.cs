using System.Collections.Concurrent;
using System.Reflection;
using Mediator.Core.Pipelines;
using Mediator.Core.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Mediator.PipelineOptimization.Pipelines;

public class PipelineFactory : IPipelineFactory
{
    private readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), Delegate> _cache = new();

    private static readonly MethodInfo BuildMethod =
        typeof(PipelineFactory)
            .GetMethod(nameof(Build), BindingFlags.Static | BindingFlags.NonPublic)
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
        return (serviceProvider, handlerObject, request, cancellationToken) =>
        {
            var services = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var pipelines = services as IPipelineBehavior<TRequest, TResponse>[] ?? services.ToArray();

            if (pipelines.Length == 0)
                return handlerInvoker(handlerObject, request, cancellationToken);

            return RunPipeline(pipelines, handlerInvoker, handlerObject, request, cancellationToken);
        };
    }

    private static Task<TResponse> RunPipeline<TRequest, TResponse>(
        IPipelineBehavior<TRequest, TResponse>[] pipelines,
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker,
        object handlerObject,
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        int index = -1;

        Task<TResponse> Next()
        {
            index++;

            return index < pipelines.Length
                ? pipelines[index].HandleAsync((TRequest)request, cancellationToken, Next)
                : handlerInvoker(handlerObject, request, cancellationToken);
        }

        return Next();
    }
}