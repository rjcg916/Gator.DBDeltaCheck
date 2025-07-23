namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface ICleanupStrategyFactory
{
    /// <summary>
    /// Creates an instance of a cleanup strategy based on its registered name.
    /// </summary>
    /// <param name="strategyName">The unique name of the strategy (e.g., "Respawn").</param>
    /// <returns>An instance of the requested ICleanupStrategy.</returns>
    ICleanupStrategy Create(string strategyName);
}