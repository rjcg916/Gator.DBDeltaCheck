using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;

public class AssertionStrategyFactory : IAssertionStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AssertionStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Gets an instance of a comparison strategy based on its registered name.
    /// </summary>
    public IAssertionStrategy GetStrategy(string strategyName)
    {
        var strategies = _serviceProvider.GetServices<IAssertionStrategy>();

        var strategy = strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));

        if (strategy == null)
            throw new ArgumentException(
                $"No assertion strategy with the name '{strategyName}' has been registered in the dependency injection container.");

        return strategy;
    }
}