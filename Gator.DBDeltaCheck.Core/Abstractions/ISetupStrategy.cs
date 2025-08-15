using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

// Represents a setup action (e.g., seeding data)
public interface ISetupStrategy
{
    string StrategyName { get; }
    Task ExecuteAsync(JObject parameters, Dictionary<string, object> testContext, DataMap? dataMap);
}