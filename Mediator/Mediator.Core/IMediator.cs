using Mediator.Core.Requests;

namespace Mediator.Core;

public interface IMediator
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);
}