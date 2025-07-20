namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface ISetupStrategyFactory { ISetupStrategy Create(string name);
    object GetStrategy(string type);
}
