namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;
public interface IComparisonStrategyFactory
{
    /// <summary>
    /// Gets an instance of a comparison strategy based on its registered name.
    /// </summary>
    /// <param name="strategyName">The unique name of the strategy (e.g., "IgnoreColumns").</param>
    /// <returns>An instance of the requested IComparisonStrategy.</returns>
    IComparisonStrategy GetStrategy(string strategyName);
}