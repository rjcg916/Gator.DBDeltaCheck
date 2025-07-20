using Gator.DBDeltaCheck.Core.Abstractions;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions;
public class ApiCallActionStrategy : IActionStrategy
{
    public string StrategyName => "ApiCall";
    // ... implementation to call a generic API endpoint
    public Task<object> ExecuteAsync(object parameters) => Task.FromResult<object>(new { });
}
