using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface ICleanupStrategyFactory
{
    ICleanupStrategy GetStrategy(string cleanupType);
}

