namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface ISetupStrategyFactory
{
    /// <summary>
    ///     Creates an instance of a setup strategy based on its registered name.
    /// </summary>
    /// <param name="strategyName">The unique name of the strategy (e.g., "HierarchicalSeed").</param>
    /// <returns>An instance of the requested ISetupStrategy.</returns>
    ISetupStrategy Create(string strategyName);
}