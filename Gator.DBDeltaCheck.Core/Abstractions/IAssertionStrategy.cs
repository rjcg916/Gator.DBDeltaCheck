using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IAssertionStrategy
{
    string StrategyName { get; }

    Task ExecuteAsync(JObject parameters, Dictionary<string, object> context, DataMap? dataMap);
}