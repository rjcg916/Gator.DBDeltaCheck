using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;

public class DataMapper(ResolveToDbStrategy resolveToDbStrategy, MapToFriendlyStrategy mapToFriendlyStrategy, IDbSchemaService schemaService)
    : IDataMapper
{
    // The DataMapper now depends on the specific strategy implementations.

    public Task<string> ResolveToDbState(string templateJson, DataMap? dataMap, string tableName)
    {
        return ProcessAsync(templateJson, dataMap, tableName, resolveToDbStrategy, false);
    }

    public Task<string> MapToFriendlyState(string dbJson, DataMap dataMap, string tableName, bool excludeDefaults = false)
    {
        return ProcessAsync(dbJson, dataMap, tableName, mapToFriendlyStrategy, excludeDefaults);
    }

    private async Task<string> ProcessAsync(string sourceJson, DataMap? dataMap, string tableName,
        IMappingStrategy strategy, bool excludeDefaults)
    {
        var token = JToken.Parse(sourceJson).DeepClone();
        await ProcessToken(token, dataMap, tableName, strategy, excludeDefaults);
        return token.ToString();
    }


    private async Task ProcessToken(JToken token, DataMap? dataMap, string tableName, IMappingStrategy strategy, bool excludeDefaults)
    {
        var tableMap = dataMap?.Tables.FirstOrDefault(t => t.Name.Equals(tableName, System.StringComparison.OrdinalIgnoreCase));

        if (token is JArray array)
        {
            foreach (var item in array.Children<JObject>())
            {
                // Recursively process each item in the array.
                await ProcessToken(item, dataMap, tableName, strategy, excludeDefaults);
            }
        }
        else if (token is JObject obj)
        {
            // 1. Apply the mapping strategy to the current object.
            await strategy.Apply(obj, tableName, tableMap);

            // 2. After mapping, find and process any nested child objects/arrays.
            var schemaRelations = await schemaService.GetChildTablesAsync(tableName);
            foreach (var relation in schemaRelations)
            {
                var childProperty = obj.Property(relation.ChildCollectionName, System.StringComparison.OrdinalIgnoreCase);
                if (childProperty?.Value != null)
                {
                    // Recursively call the main processing method for the child data.
                    await ProcessToken(childProperty.Value, dataMap, relation.ChildTableName, strategy, excludeDefaults);

                    // After processing the children, if we are creating a "friendly" state,
                    // remove the parent's foreign key from them. It's redundant in a hierarchy.
                    if (strategy is MapToFriendlyStrategy && childProperty.Value is JArray childArray)
                    {
                        // Get the name of the FK column on the child table that refers back to the parent.
                        var fkColumn = await schemaService.GetForeignKeyColumnNameAsync(relation.ChildTableName, tableName);
                        if (!string.IsNullOrEmpty(fkColumn))
                        {
                            foreach (var childItem in childArray.Children<JObject>())
                            {
                                childItem.Property(fkColumn, System.StringComparison.OrdinalIgnoreCase)?.Remove();
                            }
                        }
                    }
                }
            }

            // 3. Handle default property exclusion if required.
            if (excludeDefaults && strategy is MapToFriendlyStrategy)
            {
                await RemoveDefaultProperties(obj, tableName, tableMap);
            }
        }
    }
    /// <summary>
    /// A helper method to remove properties that have database defaults.
    /// </summary>
    private async Task RemoveDefaultProperties(JObject record, string tableName, TableMap? tableMap)
    {
        var propertiesWithDefaults = await schemaService.GetPropertyNamesWithDefaultsAsync(tableName);
        var mappedProperties = (tableMap?.Lookups ?? new()).Select(r => r.DataProperty);

        foreach (var propName in propertiesWithDefaults)
        {
            // We only remove simple scalar properties, not the ones that were just mapped from foreign keys.
            if (!mappedProperties.Contains(propName, System.StringComparer.OrdinalIgnoreCase))
            {
                record.Property(propName, System.StringComparison.OrdinalIgnoreCase)?.Remove();
            }
        }
    }
}