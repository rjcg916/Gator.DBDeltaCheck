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
        if (tableMap?.Lookups == null)
        {
            return;
        }

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
                    // 1. Before adding the new "friendly" property, remove the original
                    //    complex object property that EF Core generated.
                    record.Property(rule.DataProperty, System.StringComparison.OrdinalIgnoreCase)?.Remove();

                    // 2. Now, replace the foreign key ID property (e.g., "CustomerId")
                    //    with the new "friendly" property (e.g., "Customer").
                    property.Replace(new JProperty(rule.DataProperty, displayValue));
                }
            }
        }
    }
}
