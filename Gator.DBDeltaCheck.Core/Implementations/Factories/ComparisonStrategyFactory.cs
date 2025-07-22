using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;

namespace DBDeltaCheck.Core.ComparisonStrategies;
public class ComparisonStrategyFactory : IComparisonStrategyFactory
{
    public IComparisonStrategy GetStrategy(string name)
    {
        throw new NotImplementedException();
    }

    IComparisonStrategy IComparisonStrategyFactory.GetStrategy(string name)
    {
        throw new NotImplementedException();
    }
}
