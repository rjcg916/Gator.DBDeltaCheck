using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;

public class CleanupStrategyFactory : ICleanupStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public CleanupStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Creates an instance of a cleanup strategy based on its registered name.
    /// </summary>
    public ICleanupStrategy GetStrategy(string strategyName)
    {
        // 1. Get all registered cleanup strategies.
        var strategies = _serviceProvider.GetServices<ICleanupStrategy>();

        // 2. Find the correct one by name, ignoring case.
        var strategy = strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));

        // 3. If not found, throw a clear exception.
        if (strategy == null)
            throw new ArgumentException(
                $"No cleanup strategy with the name '{strategyName}' has been registered in the dependency injection container.");

        return strategy;
    }
}