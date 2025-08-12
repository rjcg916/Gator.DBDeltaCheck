using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IDataComparisonRule
{
    string StrategyName { get; }

    bool Compare(string actualJson, string expectedJson, object? ruleParameters);
}