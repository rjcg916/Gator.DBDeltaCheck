using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class DataMapper : IDataMapper
{
    private enum MapDirection { ToDb, ToFriendly }

    private readonly IDatabaseRepository _repository;
    private readonly IDbSchemaService _schemaService;

    public DataMapper(IDatabaseRepository repository, IDbSchemaService schemaService)
    {
        _repository = repository;
        _schemaService = schemaService;
    }

    public Task<string> ResolveToDbState(string templateJson, DataMap? dataMap, string tableName)
    {
        return ProcessAsync(templateJson, dataMap, tableName, MapDirection.ToDb);
    }

    public Task<string> MapToFriendlyState(string dbJson, DataMap dataMap, string tableName)
    {
        return ProcessAsync(dbJson, dataMap, tableName, MapDirection.ToFriendly);
    }

    /// <summary>
    /// The single, private helper method that contains the shared logic.
    /// </summary>
    private async Task<string> ProcessAsync(string sourceJson, DataMap? dataMap, string tableName, MapDirection direction)
    {
        var token = JToken.Parse(sourceJson).DeepClone();
        var tableMap = dataMap?.Tables.FirstOrDefault(t => t.Name.Equals(tableName, System.StringComparison.OrdinalIgnoreCase));

        if (token is JArray array)
        {
            foreach (var item in array.Children<JObject>())
            {
                await ProcessRecord(item, tableName, tableMap, direction);
            }
        }
        else if (token is JObject obj)
        {
            await ProcessRecord(obj, tableName, tableMap, direction);
        }

        return token.ToString();
    }

    private async Task ProcessRecord(JObject record, string tableName, TableMap? tableMap, MapDirection direction)
    {

        // This pattern ensures that we always have a valid list to call ToDictionary on.
        // If tableMap or its Lookups property is null, it defaults to an empty list.
        var lookupRules = (tableMap?.Lookups ?? new List<LookupRule>())
            .ToDictionary(r => r.DataProperty, r => r, StringComparer.OrdinalIgnoreCase);

        foreach (var property in record.Properties().ToList())
        {
            if (direction == MapDirection.ToDb) // Resolving: Friendly Name -> ID
            {
                string? fkColumnName = null;
                object? resolvedId = null;

                // PRIORITY 1: Check for a rule in the data map.
                if (lookupRules.TryGetValue(property.Name, out var rule))
                {
                    var displayValue = property.Value.ToObject<object>();
                    fkColumnName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
                    resolvedId = await ResolveValue(rule.LookupTable, rule.LookupValueColumn, displayValue);
                }
                // PRIORITY 2: Fall back to checking for an inline _lookup object.
                else if (property.Value is JObject lookupObject && lookupObject.TryGetValue("_lookupTable", out var lookupTableToken))
                {
                    var lookupTableName = lookupTableToken.Value<string>();
                    var valueColumn = lookupObject["_lookupValueColumn"].Value<string>();
                    var displayValue = lookupObject["_lookupDisplayValue"].ToObject<object>();

                    fkColumnName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, lookupTableName);
                    resolvedId = await ResolveValue(lookupTableName, valueColumn, displayValue);
                }

                if (fkColumnName != null && resolvedId != null)
                {
                    property.Replace(new JProperty(fkColumnName, resolvedId));
                }
            }
            else // Mapping: ID -> Friendly Name
            {
                if (tableMap == null) continue;

                foreach (var mapRule in tableMap.Lookups)
                {
                    var fkColumnName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, mapRule.LookupTable);
                    if (property.Name.Equals(fkColumnName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        var idValue = property.Value.ToObject<object>();
                        var pkColumn = await _schemaService.GetPrimaryKeyColumnNameAsync(mapRule.LookupTable);
                        var sql = $"SELECT {mapRule.LookupValueColumn} FROM {mapRule.LookupTable} WHERE {pkColumn} = @idValue";
                        var displayValue = await _repository.ExecuteScalarAsync<object>(sql, new { idValue });

                        if (displayValue != null)
                        {
                            property.Replace(new JProperty(mapRule.DataProperty, displayValue));
                        }
                    }
                }
            }
        }
    }

    private async Task<object?> ResolveValue(string lookupTable, string lookupValueColumn, object displayValue)
    {
        var pkColumn = await _schemaService.GetPrimaryKeyColumnNameAsync(lookupTable);
        var sql = $"SELECT {pkColumn} FROM {lookupTable} WHERE {lookupValueColumn} = @displayValue";
        var resolvedId = await _repository.ExecuteScalarAsync<object>(sql, new { displayValue });

        if (resolvedId == null)
            throw new System.InvalidOperationException($"Lookup failed: Could not find record in '{lookupTable}' where '{lookupValueColumn}' is '{displayValue}'.");

        return resolvedId;
    }
}
