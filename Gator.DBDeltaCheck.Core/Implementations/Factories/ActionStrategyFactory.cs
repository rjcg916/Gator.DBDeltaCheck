using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Implementations.Actions;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;
public class ActionStrategyFactory : IActionStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    // The factory gets the DI container's service provider to resolve instances
    public ActionStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IActionStrategy GetStrategy(string actionType)
    {
        // In a real application, you would register these types with your DI container.
        // For example, in Startup.cs:
        // services.AddTransient<DurableFunctionActionStrategy>();
        // services.AddTransient<ApiCallActionStrategy>();

        return actionType switch
        {
            "DurableFunction" => (IActionStrategy)_serviceProvider.GetService(typeof(DurableFunctionActionStrategy)),
            "ApiCall" => (IActionStrategy)_serviceProvider.GetService(typeof(ApiCallActionStrategy)),
            _ => throw new NotSupportedException($"Action type '{actionType}' is not supported.")
        };
    }
}