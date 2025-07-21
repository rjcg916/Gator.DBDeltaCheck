using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;
public class CleanupStrategyFactory : ICleanupStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    public CleanupStrategyFactory(IServiceProvider serviceProvider) { _serviceProvider = serviceProvider; }
    public ICleanupStrategy Create(string name) => _serviceProvider.GetServices<ICleanupStrategy>().First(s => s.StrategyName == name);

    public ICleanupStrategy GetStrategy(string cleanupType)
    {
        throw new NotImplementedException();
    }
}