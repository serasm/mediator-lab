using System.Collections.Concurrent;
using System.Linq.Expressions;
using Mediator.Core;
using Mediator.Core.Requests;

namespace Mediator.HandlersCache;

public class MediatorHandlersCache : IMediator
{
    private readonly IServiceProvider _serviceProvider;
    
    private readonly ConcurrentDictionary<Type, (Type RequestHandlerType, Delegate Invoker)> _requestHandlerInvokers =
        new();

    public MediatorHandlersCache(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var (handlerType, invokerObj) =
            _requestHandlerInvokers.GetOrAdd(requestType, BuildRequestHandlerInfo<TResponse>(requestType));
        
        var invoker = (Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>>)invokerObj;

        var handler = _serviceProvider.GetService(handlerType)
                      ?? throw new InvalidOperationException($"No handler registered for type {handlerType.Name}");

        return invoker(handler, request, cancellationToken);
    }
    
    private static (Type RequestHandlerType, Delegate Invoker) BuildRequestHandlerInfo<TResponse>(Type requestType)
    {
        var responseType = typeof(TResponse);
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handleMethod = handlerType.GetMethod("HandleAsync")
                           ?? throw new InvalidOperationException(
                               $"No handle method registered for type {handlerType.Name}");
        
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var requestParam = Expression.Parameter(typeof(IRequest<TResponse>), "request");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        
        var body = Expression.Call(
            Expression.Convert(handlerParam, handlerType),
            handleMethod,
            Expression.Convert(requestParam, requestType),
            ctParam);

        var lambda =
            Expression.Lambda<Func<object, IRequest<TResponse>, CancellationToken, Task<TResponse>>>(body, handlerParam,
                requestParam, ctParam);
        
        return (handlerType, lambda.Compile());
    }
}