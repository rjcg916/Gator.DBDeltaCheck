using Gator.DBDeltaCheck.Core.Abstractions;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;
public class JsonFileSeedingStrategy : ISetupStrategy
{
    public string StrategyName => "JsonFileSeed";
    // ... implementation to read a JSON file and insert data
    public Task ExecuteAsync(object parameters) => Task.CompletedTask;
}
