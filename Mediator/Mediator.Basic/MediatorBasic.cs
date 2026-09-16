using System.Linq.Expressions;
using Mediator.Core;
using Mediator.Core.Requests;

namespace Mediator.Basic;

public class MediatorBasic : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    public MediatorBasic(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        var handlerType = typeof(IRequestHandler<,>)
            .MakeGenericType(requestType, responseType);

        var handler = _serviceProvider.GetService(handlerType)
                      ?? throw new InvalidOperationException(
                          $"No handler registered for type {handlerType.Name}");

        var handleMethod = handlerType.GetMethod("HandleAsync")
                           ?? throw new InvalidOperationException(
                               $"No handle method registered for type {handlerType.Name}");

        var result = handleMethod.Invoke(
            handler,
            [request, cancellationToken]);

        return (Task<TResponse>)result!;
    }
}