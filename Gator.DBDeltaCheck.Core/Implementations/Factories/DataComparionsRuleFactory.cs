using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;

public class DataComparisonRuleFactory(IServiceProvider serviceProvider) : IDataComparisonRuleFactory
{
    /// <summary>
    ///     Gets an instance of a comparison strategy based on its registered name.
    /// </summary>
    public IDataComparisonRule GetStrategy(string strategyName)
    {
        var strategies = serviceProvider.GetServices<IDataComparisonRule>();

        var strategy = strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));

        if (strategy == null)
            throw new ArgumentException(
                $"No assertion strategy with the name '{strategyName}' has been registered in the dependency injection container.");

        return strategy;
    }
}