using Microsoft.Extensions.DependencyInjection;

namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface ICleanupStrategyFactory
{
    public ICleanupStrategy Create(string name);
}

