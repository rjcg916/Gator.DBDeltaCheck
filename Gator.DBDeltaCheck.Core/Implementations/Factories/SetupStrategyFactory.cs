using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;
public class SetupStrategyFactory : ISetupStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    public SetupStrategyFactory(IServiceProvider serviceProvider) { _serviceProvider = serviceProvider; }
    public ISetupStrategy Create(string name) => _serviceProvider.GetServices<ISetupStrategy>().First(s => s.StrategyName == name);

    public object GetStrategy(string type)
    {
        throw new NotImplementedException();
    }
}
