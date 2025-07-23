using FluentAssertions;
using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json;


namespace DBDeltaCheck.Core.Implementations.Comparisons;

public class IgnoreOrderComparisonStrategy : IComparisonStrategy
{
    public string StrategyName => "IgnoreOrder";

    public bool Compare(string beforeStateJson, string afterStateJson, string expectedStateJson, object? parameters)
    {
        var afterList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(afterStateJson);
        var expectedList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(expectedStateJson);

        // FluentAssertions' BeEquivalentTo handles collection comparisons irrespective of order by default.
        // It checks that all items in one collection exist in the other, and vice-versa.
        try
        {
            afterList.Should().BeEquivalentTo(expectedList);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
