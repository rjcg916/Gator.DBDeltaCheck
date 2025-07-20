namespace Gator.DBDeltaCheck.Core.Abstractions;

// Represents the main action of the test (e.g., calling a function)
public interface IActionStrategy
{
    string StrategyName { get; }
    Task<object> ExecuteAsync(object parameters);
}