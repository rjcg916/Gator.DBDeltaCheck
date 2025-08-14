using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;

public class DataMapper(ResolveToDbStrategy resolveToDbStrategy, MapToFriendlyStrategy mapToFriendlyStrategy)
    : IDataMapper
{
    // The DataMapper now depends on the specific strategy implementations.

    public Task<string> ResolveToDbState(string templateJson, DataMap? dataMap, string tableName)
    {
        return ProcessAsync(templateJson, dataMap, tableName, resolveToDbStrategy);
    }

    public Task<string> MapToFriendlyState(string dbJson, DataMap dataMap, string tableName)
    {
        return ProcessAsync(dbJson, dataMap, tableName, mapToFriendlyStrategy);
    }

    // The 'direction' enum is gone. We now pass the strategy object itself.
    private async Task<string> ProcessAsync(string sourceJson, DataMap? dataMap, string tableName,
        IMappingStrategy strategy)
    {
        var token = JToken.Parse(sourceJson).DeepClone();
        var tableMap =
            dataMap?.Tables.FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

        // If there's no map for this table, there's nothing to do.
        if (tableMap == null) return sourceJson;

        if (token is JArray array)
        {
            foreach (var item in array.Children<JObject>())
            {
                await strategy.Apply(item, tableName, tableMap);
            }
        }
        else if (token is JObject obj)
        {
            await strategy.Apply(obj, tableName, tableMap);
        }

        return token.ToString();
    }
}