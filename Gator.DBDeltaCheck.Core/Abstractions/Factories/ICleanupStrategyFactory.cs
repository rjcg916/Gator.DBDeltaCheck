namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface ICleanupStrategyFactory
{
    ICleanupStrategy GetStrategy(string cleanupTypeName);
}
