using System.Collections.Concurrent;
using System.Reflection;
using Mediator.Core;

namespace Mediator.HandlersCache.Tests;

public static class MediatorCacheHelper
{
    public static ConcurrentDictionary<Type, (Type RequestHandlerType, Delegate Invoker)> GetRequestHandlerCache(
        MediatorHandlersCache mediator)
    {
        var field = typeof(MediatorHandlersCache).GetField(
            "_cache",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        
        var value = field!.GetValue(mediator);
        Assert.NotNull(value);
        
        return Assert.IsType<ConcurrentDictionary<Type, (Type RequestHandlerType, Delegate Invoker)>>(value);
    }
}