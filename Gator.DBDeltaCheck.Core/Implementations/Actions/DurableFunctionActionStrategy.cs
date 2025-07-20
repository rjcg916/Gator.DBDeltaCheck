using Gator.DBDeltaCheck.Core.Abstractions;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions;
public class DurableFunctionActionStrategy : IActionStrategy
{
    public string StrategyName => "DurableFunction";
    // ... implementation to call a durable function
    public Task<object> ExecuteAsync(object parameters) => Task.FromResult<object>(new { });
}
