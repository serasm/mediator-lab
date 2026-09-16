using Mediator.Core.Requests;

namespace Mediator.Core;

public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);
}