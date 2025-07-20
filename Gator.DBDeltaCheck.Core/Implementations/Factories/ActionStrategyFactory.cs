using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Implementations.Factories;
public class ActionStrategyFactory : IActionStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    public ActionStrategyFactory(IServiceProvider serviceProvider) { _serviceProvider = serviceProvider; }
    public IActionStrategy Create(string name) => _serviceProvider.GetServices<IActionStrategy>().First(s => s.StrategyName == name);
}