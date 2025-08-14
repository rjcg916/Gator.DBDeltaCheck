using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;

public class SetupStrategyFactory(IServiceProvider serviceProvider) : ISetupStrategyFactory
{
    /// <summary>
    ///     Creates an instance of a setup strategy based on its registered name.
    /// </summary>
    public ISetupStrategy GetStrategy(string strategyName)
    {
        var strategies = serviceProvider.GetServices<ISetupStrategy>();


        var strategy = strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));


        if (strategy == null)
            throw new ArgumentException(
                $"No setup strategy with the name '{strategyName}' has been registered in the dependency injection container.");

        return strategy;
    }
}