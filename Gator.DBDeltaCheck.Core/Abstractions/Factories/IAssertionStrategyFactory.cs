namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface IAssertionStrategyFactory
{
    /// <summary>
    /// Gets an instance of an assertion strategy based on its registered name.
    /// </summary>
    /// <param name="strategyName">The unique name of the strategy (e.g., "Flat", "Hierarchical").</param>
    /// <returns>An instance of the requested IAssertionStrategy.</returns>
    IAssertionStrategy GetStrategy(string strategyName);
}