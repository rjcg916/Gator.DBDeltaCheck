using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;

public class DataMapper(ResolveToDbStrategy resolveToDbStrategy, MapToFriendlyStrategy mapToFriendlyStrategy, IDbSchemaService schemaService)
    : IDataMapper
{

    public Task<string> ResolveToDbState(string templateJson, DataMap? dataMap, string tableName, Dictionary<string, string> columnOverrides)
    {
        return ProcessAsync(templateJson, dataMap, tableName, resolveToDbStrategy, columnOverrides, false);
    }

    public Task<string> MapToFriendlyState(string dbJson, DataMap dataMap, string tableName, Dictionary<string, string> columnOverrides, bool excludeDefaults = false)
    {
        return ProcessAsync(dbJson, dataMap, tableName, mapToFriendlyStrategy, columnOverrides, excludeDefaults);
    }

    private async Task<string> ProcessAsync(string sourceJson, DataMap? dataMap, string tableName,
        IMappingStrategy strategy, Dictionary<string, string> columnOverrides, bool excludeDefaults)
    {
        var token = JToken.Parse(sourceJson).DeepClone();
        await ProcessToken(token, dataMap, tableName, strategy, columnOverrides, excludeDefaults);
        return token.ToString();
    }


    private async Task ProcessToken(JToken token, DataMap? dataMap, string tableName, IMappingStrategy strategy, Dictionary<string, string> columnOverrides, bool excludeDefaults)
    {
        var tableMap = dataMap?.Tables.FirstOrDefault(t => t.Name.Equals(tableName, System.StringComparison.OrdinalIgnoreCase));

        switch (token)
        {
            case JArray array:
                {
                    foreach (var item in array.Children<JObject>())
                    {
                        await ProcessToken(item, dataMap, tableName, strategy, columnOverrides, excludeDefaults);
                    }

                    break;
                }
            case JObject obj:
                {
                    // 1. Apply the mapping strategy to the current object.
                    await strategy.Apply(obj, tableName, tableMap, columnOverrides);

                    // 2. After mapping, find and process any nested child objects/arrays.
                    var schemaRelations = await schemaService.GetChildTablesAsync(tableName);
                    foreach (var relation in schemaRelations)
                    {
                        var childProperty = obj.Property(relation.ChildCollectionName, System.StringComparison.OrdinalIgnoreCase);
                        if (childProperty?.Value == null) continue;

                        await ProcessToken(childProperty.Value, dataMap, relation.ChildTableName, strategy, columnOverrides, excludeDefaults);

                        // After processing the children, if we are creating a "friendly" state,
                        // remove the parent's foreign key it's redundant in a hierarchy.

                        if (strategy is not MapToFriendlyStrategy || childProperty.Value is not JArray childArray) continue;

                        // Get the name of the FK column on the child table that refers back to the parent.
                        var fkColumn = await schemaService.GetForeignKeyColumnNameAsync(relation.ChildTableName, tableName);
                        if (string.IsNullOrEmpty(fkColumn)) continue;
                        foreach (var childItem in childArray.Children<JObject>())
                        {
                            childItem.Property(fkColumn, System.StringComparison.OrdinalIgnoreCase)?.Remove();
                        }
                    }

                    // 3. Handle default property exclusion if required.
                    if (excludeDefaults && strategy is MapToFriendlyStrategy)
                    {
                        await RemoveDefaultProperties(obj, tableName, tableMap);
                    }

                    // 4. For friendly mapping, remove any eagerly loaded navigation properties
                    // that do not have a corresponding "friendly" lookup rule. This prevents
                    // redundant objects (like 'UpdateUser') from appearing alongside their
                    // foreign key IDs ('UpdatedUserId').
                    if (strategy is MapToFriendlyStrategy)
                    {
                        var entityType = await schemaService.GetEntityTypeAsync(tableName);
                        if (entityType != null)
                        {
                            foreach (var nav in entityType.GetNavigations().Where(n => !n.IsCollection))
                            {
                                var hasLookupRule = tableMap?.Lookups.Any(l => l.DataProperty.Equals(nav.Name, StringComparison.OrdinalIgnoreCase)) ?? false;
                                if (!hasLookupRule)
                                {
                                    obj.Property(nav.Name, StringComparison.OrdinalIgnoreCase)?.Remove();
                                }
                            }
                        }
                    }

                    break;
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