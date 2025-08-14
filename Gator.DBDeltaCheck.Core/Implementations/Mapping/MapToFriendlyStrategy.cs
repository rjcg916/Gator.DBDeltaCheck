using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;

/// <summary>
/// Strategy for mapping database IDs to friendly names.
/// </summary>
public class MapToFriendlyStrategy(IDbSchemaService schemaService, IDataMapperValueResolver valueResolver)
    : IMappingStrategy
{
    public async Task Apply(JObject record, string tableName, TableMap tableMap)
    {
        foreach (var rule in tableMap.Lookups)
        {
            var fkColumnName = await schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
            var property = record.Property(fkColumnName, StringComparison.OrdinalIgnoreCase);

            if (property != null)
            {
                var idValue = property.Value.ToObject<object>();
                var displayValue = await valueResolver.ResolveToFriendlyValue(rule.LookupTable, rule.LookupValueColumn, idValue);

                if (displayValue != null)
                {
                    property.Replace(new JProperty(rule.DataProperty, displayValue));
                }
            }
        }
    }
}
