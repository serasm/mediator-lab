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
            .GetMethod(
                nameof(Build),
                BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Method 'Build' not found");
    
    public Delegate GetOrAdd<TResponse>(
        Type requestType,
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker)
    {
        return _cache.GetOrAdd(
            (requestType, typeof(TResponse)),
            _ => BuildClosedPipeline<TResponse>(requestType, handlerInvoker));
    }

    private Delegate BuildClosedPipeline<TResponse>(
        Type requestType,
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker)
    {
        var method = BuildMethod.MakeGenericMethod(
            requestType,
            typeof(TResponse));
        
        return (Delegate)method.Invoke(this, new object[] { handlerInvoker });
    }

    private Func<IServiceProvider, object, IRequest<TResponse>, CancellationToken, Task<TResponse>> Build<TRequest, TResponse>(
        Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>> handlerInvoker)
        where TRequest : IRequest<TResponse>
    {
        return (serviceProvider, handlerObject, request, cancellationToken) =>
        {
            var services = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var pipelines = services as IPipelineBehavior<TRequest, TResponse>[] ?? services.ToArray();

            if (pipelines.Length == 0)
            {
                return handlerInvoker(handlerObject, request, cancellationToken);
            }

            int index = -1;

            Task<TResponse> Next()
            {
                index++;

                if (index < pipelines.Length)
                {
                    return pipelines[index]
                        .HandleAsync((TRequest)request, cancellationToken, Next);
                }
                
                return handlerInvoker(handlerObject, request, cancellationToken);
            }

            return Next();
        };
    }
}