using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Comparisons;

public class StrictEquivalenceComparisonRule : IDataComparisonRule
{
    public string StrategyName => "StrictEquivalence";

    public bool Compare(string afterStateJson, string expectedStateJson, object? parameters)
    {
        // For strict equivalence, we normalize the JSON formatting and then do a string comparison.
        // This is the most rigid check.
        var afterToken = JToken.Parse(afterStateJson);
        var expectedToken = JToken.Parse(expectedStateJson);

        return JToken.DeepEquals(afterToken, expectedToken);
    }

}