using FluentAssertions;
using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Implementations.Comparisons;

public class IgnoreColumnsComparisonRule : IDataComparisonRule
{
    public string StrategyName => "IgnoreColumns";

    public bool Compare(string afterStateJson, string expectedStateJson, object? parameters)
    {
        var afterList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(afterStateJson);
        var expectedList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(expectedStateJson);

        // The 'parameters' object is expected to be a list of column names to ignore.
        var columnsToIgnore = JsonConvert.DeserializeObject<List<string>>(parameters?.ToString() ?? "[]");

        if (columnsToIgnore == null || !columnsToIgnore.Any())
            // If no columns are specified, behave like IgnoreOrder
            return new IgnoreOrderComparisonRule().Compare(afterStateJson, expectedStateJson,
                parameters);

        // Remove the ignored columns from both datasets
        var cleanedAfterList = RemoveColumns(afterList, columnsToIgnore);
        var cleanedExpectedList = RemoveColumns(expectedList, columnsToIgnore);

        try
        {
            cleanedAfterList.Should().BeEquivalentTo(cleanedExpectedList);
            return true;
        }
        catch
        {
            return false;
        }
    }


    private static List<Dictionary<string, object>> RemoveColumns(List<Dictionary<string, object>>? data,
        List<string> columnsToRemove)
    {
        if (data == null) return [];

        foreach (var row in data)
        foreach (var column in columnsToRemove)
        {
            // Use case-insensitive matching for robustness
            var keyToRemove = row.Keys.FirstOrDefault(k => k.Equals(column, StringComparison.OrdinalIgnoreCase));
            if (keyToRemove != null) row.Remove(keyToRemove);
        }

        return data;
    }
}