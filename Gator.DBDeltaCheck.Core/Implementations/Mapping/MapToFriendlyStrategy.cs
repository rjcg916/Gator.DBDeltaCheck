using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;

public class MapToFriendlyStrategy : IMappingStrategy
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDataMapperValueResolver _valueResolver;

    public MapToFriendlyStrategy(IDbSchemaService schemaService, IDataMapperValueResolver valueResolver)
    {
        _schemaService = schemaService;
        _valueResolver = valueResolver;
    }

    public async Task Apply(JObject record, string tableName, TableMap? tableMap)
    {
        if (tableMap?.Lookups == null)
        {
            return;
        }

        var cleanRecord = new JObject();

        // Create a map from the lookup rule to its foreign key column name. This avoids redundant schema calls.
        var ruleToFkNameMap = new Dictionary<LookupRule, string>();
        foreach (var rule in tableMap.Lookups)
        {
            // Use LookupTable to find the FK relationship, as it represents the principal table.
            // DataProperty is the desired name for the new property in the output JSON.
            var fkName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
            if (!string.IsNullOrEmpty(fkName))
            {
                ruleToFkNameMap[rule] = fkName;
            }
            else
            {
                // Log a more specific warning that a relationship in the map could not be found in the schema.
                System.Console.WriteLine($"Warning: Could not find foreign key in table '{tableName}' that references table '{rule.LookupTable}'. Skipping rule for property '{rule.DataProperty}'.");
            }
        }

        // Create a map from the FK column name to its lookup rule for quick checking.
        var fkToRuleMap = ruleToFkNameMap
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, System.StringComparer.OrdinalIgnoreCase);

        // Create a set of the friendly property names (e.g., "Customer") to ignore if they exist on the source.
        var navPropertyNames = new HashSet<string>(tableMap.Lookups.Select(r => r.DataProperty), System.StringComparer.OrdinalIgnoreCase);

        // 1. Iterate through the original record's properties to copy scalars.
        foreach (var property in record.Properties())
        {
            // If the property is a foreign key (which we're replacing) or a friendly property, skip it.
            if (fkToRuleMap.ContainsKey(property.Name) || navPropertyNames.Contains(property.Name))
            {
                continue;
            }

            // Otherwise, it's a scalar property we want to keep.
            cleanRecord.Add(property.Name, property.Value);
        }

        // 2. Now, process the lookup rules to add the "friendly" properties.
        foreach (var (rule, fkColumnName) in ruleToFkNameMap)
        {
            var fkProperty = record.Property(fkColumnName, System.StringComparison.OrdinalIgnoreCase);

            if (fkProperty != null)
            {
                var idValue = fkProperty.Value.ToObject<object>();
                if (idValue != null && idValue != DBNull.Value)
                {
                    var displayValue = await _valueResolver.ResolveToFriendlyValue(rule.LookupTable, rule.LookupValueColumn, idValue);
                    if (displayValue != null)
                    {
                        cleanRecord.Add(rule.DataProperty, JToken.FromObject(displayValue));
                    }
                }
            }
        }

        // 3. Finally, replace the original record's content with the clean content.
        record.RemoveAll();
        foreach (var property in cleanRecord.Properties())
        {
            record.Add(property.Name, property.Value);
        }
    }
}
