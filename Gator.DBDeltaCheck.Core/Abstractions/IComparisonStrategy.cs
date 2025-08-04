namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IComparisonStrategy
{
    string StrategyName { get; }

    bool Compare(string beforeStateJson, string afterStateJson, string expectedStateJson, object? parameters);
}