namespace Gator.DBDeltaCheck.Core.Abstractions;

// Represents a cleanup action (e.g., deleting data)
public interface ICleanupStrategy
{
    string StrategyName { get; }
    Task ExecuteAsync(object parameters);
}