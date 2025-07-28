using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.ComparisonStrategies;

public class ComparisonStrategyFactory : IComparisonStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ComparisonStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets an instance of a comparison strategy based on its registered name.
    /// </summary>
    public IComparisonStrategy GetStrategy(string strategyName)
    {

        var strategies = _serviceProvider.GetServices<IComparisonStrategy>();

        var strategy = strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));

        if (strategy == null)
        {
            throw new ArgumentException($"No comparison strategy with the name '{strategyName}' has been registered in the dependency injection container.");
        }

        return strategy;
    }
}
