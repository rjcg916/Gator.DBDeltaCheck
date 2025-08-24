using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;


/// <summary>
/// Strategy for resolving friendly names to database IDs.
/// </summary>
public class ResolveToDbStrategy(IDbSchemaService schemaService, IDataMapperValueResolver valueResolver)
    : IMappingStrategy
{
    public async Task Apply(JObject record, string tableName, TableMap? tableMap)
    {
        var lookupRules = (tableMap?.Lookups ?? new System.Collections.Generic.List<LookupRule>())
            .ToDictionary(r => r.DataProperty, r => r, System.StringComparer.OrdinalIgnoreCase);

        foreach (var property in record.Properties().ToList())
        {
            string? fkColumnName = null;
            object? resolvedId = null;

            if (lookupRules.TryGetValue(property.Name, out var rule))
            {
                var displayValue = property.Value.ToObject<object>();
                fkColumnName = await schemaService.GetForeignKeyColumnNameAsync(tableName, rule.DataProperty);
                resolvedId = await valueResolver.ResolveToId(rule.LookupTable, rule.LookupValueColumn, displayValue);
            }
            else if (property.Value is JObject lookupObject && lookupObject.TryGetValue("_lookupTable", out var lookupTableToken))
            {
                var lookupTableName = lookupTableToken.Value<string>();
                var valueColumn = lookupObject["_lookupValueColumn"].Value<string>();
                var displayValue = lookupObject["_lookupDisplayValue"].ToObject<object>();
                var dataProperty = lookupObject["_dataProperty"].ToObject<string>();

                fkColumnName = await schemaService.GetForeignKeyColumnNameAsync(tableName, dataProperty);
                resolvedId = await valueResolver.ResolveToId(lookupTableName, valueColumn, displayValue);
            }

            if (fkColumnName != null && resolvedId != null)
            {
                property.Replace(new JProperty(fkColumnName, resolvedId));
            }
        }
    }
}