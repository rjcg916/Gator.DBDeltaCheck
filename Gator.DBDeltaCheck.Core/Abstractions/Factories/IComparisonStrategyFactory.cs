namespace DBDeltaCheck.Core.Abstractions.Factories;
public interface IComparisonStrategyFactory
{
    public IComparisonStrategy GetStrategy(string name);
}
