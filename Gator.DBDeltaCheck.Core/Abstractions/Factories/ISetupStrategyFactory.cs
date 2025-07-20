namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface ISetupStrategyFactory
{
    ISetupStrategy GetStrategy(string setupType);
}
