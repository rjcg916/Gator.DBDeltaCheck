using FluentAssertions;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Implementations.Comparisons;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Assertions;

public class FlatAssertionStrategy(
    IDatabaseRepository dbRepository,
    IDataMapper dataMapper,
    IDataComparisonRuleFactory ruleFactory)
    : IAssertionStrategy
{
    public string StrategyName => "FlatAssert";

    public async Task ExecuteAsync(JObject parameters, Dictionary<string, object> context, DataMap? dataMap)
    {

        // 1. Get all parameters from the JSON.

        var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();
        var expectedDataFile = parameters["ExpectedDataFile"]?.Value<string>()
                               ?? throw new ArgumentException("'ExpectedDataFile' is missing from Flat assertion parameters.");

        var tableName = parameters["TableName"]?.Value<string>()
            ?? throw new ArgumentException("'TableName' is missing from Flat assertion parameters.");


        var comparisonRuleInfo = parameters["ComparisonRule"]?.ToObject<IDataComparisonRule>()
            ?? new IgnoreOrderComparisonRule(); // Default to IgnoreOrder

        // 2. Get the raw actual state from the database.
        var actualData = await dbRepository.QueryAsync<object>($"SELECT * FROM {tableName}");
        var rawActualStateJson = JsonConvert.SerializeObject(actualData);

        // 3. Load the expected state file.
        var expectedDataPath = Path.Combine(basePath, expectedDataFile);
        var expectedStateJson = await File.ReadAllTextAsync(expectedDataPath);

        string finalActualStateJson = rawActualStateJson;

        // 4. If a data map is provided, transform the ACTUAL data into the "friendly" format.
        if (dataMap != null)
        {
            finalActualStateJson = await dataMapper.MapToFriendlyState(rawActualStateJson, dataMap, tableName);
        }

        // 5. Get the correct comparison rule and execute it.
        var comparisonRule = ruleFactory.GetStrategy(comparisonRuleInfo.StrategyName);
        var areEqual = comparisonRule.Compare(finalActualStateJson, expectedStateJson, parameters);

        // 6. Use FluentAssertions for a clear pass/fail message.
        areEqual.Should().BeTrue($"Flat comparison failed for table '{tableName}'.");
    }
}
