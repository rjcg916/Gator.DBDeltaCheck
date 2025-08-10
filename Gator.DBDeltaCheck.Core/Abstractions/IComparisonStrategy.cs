using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IComparisonStrategy
{
    string StrategyName { get; }

    bool Compare(string beforeStateJson, string afterStateJson, string expectedStateJson, object? parameters);

    Task<bool> ExecuteAsync(JObject parameters, Dictionary<string, object> context, DataMap? dataMap);

}