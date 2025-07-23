using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

// Represents the main action of the test (e.g., calling a function)
public interface IActionStrategy
{
    /// <summary>
    /// Executes the action based on the provided configuration.
    /// </summary>
    /// <param name="config">The JObject containing configuration specific to this action.</param>
    Task<bool> ExecuteAsync(JObject config);
}