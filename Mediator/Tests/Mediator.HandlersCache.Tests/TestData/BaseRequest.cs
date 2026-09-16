using Mediator.Core.Requests;

namespace Mediator.HandlersCache.Tests.TestData;

public record BaseRequest(int value) : IRequest<int>;

public class BaseRequestHandler : IRequestHandler<BaseRequest, int>
{
    public Task<int> HandleAsync(BaseRequest query, CancellationToken cancellationToken)
    {
        return Task.FromResult(999);
    }
}