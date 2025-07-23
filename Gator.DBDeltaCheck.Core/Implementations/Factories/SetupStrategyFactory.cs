using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;


public class SetupStrategyFactory : ISetupStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public SetupStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates an instance of a setup strategy based on its registered name.
    /// </summary>
    public ISetupStrategy Create(string strategyName)
    {

        var strategies = _serviceProvider.GetServices<ISetupStrategy>();


        var strategy = strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));


        if (strategy == null)
        {
            throw new ArgumentException($"No setup strategy with the name '{strategyName}' has been registered in the dependency injection container.");
        }

        return strategy;
    }
}