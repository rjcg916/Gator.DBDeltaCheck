namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface IActionStrategyFactory { 
    IActionStrategy GetStrategy(string actionTypeName); 
}
