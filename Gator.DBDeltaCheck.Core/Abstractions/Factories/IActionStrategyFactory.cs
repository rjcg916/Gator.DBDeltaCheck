namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;
public interface IActionStrategyFactory
{
    /// <summary>
    /// Gets an instance of an action strategy based on its registered name.
    /// </summary>
    /// <param name="strategyName">The unique name of the strategy (e.g., "DurableFunction").</param>
    /// <returns>An instance of the requested IActionStrategy.</returns>
    IActionStrategy GetStrategy(string strategyName);
}