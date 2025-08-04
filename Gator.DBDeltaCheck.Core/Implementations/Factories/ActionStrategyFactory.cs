using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;

public class ActionStrategyFactory : IActionStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ActionStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Gets an instance of an action strategy based on its registered name.
    ///     This implementation dynamically resolves the strategy from the DI container.
    /// </summary>
    public IActionStrategy GetStrategy(string strategyName)
    {
        var strategies = _serviceProvider.GetServices<IActionStrategy>();

        var strategy = strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));


        if (strategy == null)
            throw new NotSupportedException(
                $"Action strategy '{strategyName}' is not supported or has not been registered in the dependency injection container.");

        return strategy;
    }
}