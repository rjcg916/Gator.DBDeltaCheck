using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

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

    public Task<string> ResolveToDbState(string templateJson, DataMap dataMap, string tableName)
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
    private async Task<string> ProcessAsync(string sourceJson, DataMap dataMap, string tableName, MapDirection direction)
    {
        var token = JToken.Parse(sourceJson).DeepClone();
        var tableMap = dataMap.Tables.FirstOrDefault(t => t.Name.Equals(tableName, System.StringComparison.OrdinalIgnoreCase));

        if (tableMap == null) return sourceJson;

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

    private async Task ProcessRecord(JObject record, string tableName, TableMap tableMap, MapDirection direction)
    {
        foreach (var rule in tableMap.Lookups)
        {
            if (direction == MapDirection.ToDb) // Resolving: Friendly Name -> ID
            {
                var property = record.Property(rule.DataProperty, System.StringComparison.OrdinalIgnoreCase);
                if (property != null)
                {
                    var displayValue = property.Value.ToObject<object>();
                    var pkColumn = await _schemaService.GetPrimaryKeyColumnNameAsync(rule.LookupTable);
                    var sql = $"SELECT {pkColumn} FROM {rule.LookupTable} WHERE {rule.LookupValueColumn} = @displayValue";
                    var resolvedId = await _repository.ExecuteScalarAsync<object>(sql, new { displayValue });

                    if (resolvedId == null)
                        throw new System.InvalidOperationException($"Lookup failed: Could not find record in '{rule.LookupTable}' where '{rule.LookupValueColumn}' is '{displayValue}'.");

                    var fkColumnName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
                    property.Replace(new JProperty(fkColumnName, resolvedId));
                }
            }
            else // Mapping: ID -> Friendly Name
            {
                var fkColumnName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
                var property = record.Property(fkColumnName, System.StringComparison.OrdinalIgnoreCase);
                if (property != null)
                {
                    var idValue = property.Value.ToObject<object>();
                    var pkColumn = await _schemaService.GetPrimaryKeyColumnNameAsync(rule.LookupTable);
                    var sql = $"SELECT {rule.LookupValueColumn} FROM {rule.LookupTable} WHERE {pkColumn} = @idValue";
                    var displayValue = await _repository.ExecuteScalarAsync<object>(sql, new { idValue });

                    if (displayValue != null)
                    {
                        property.Replace(new JProperty(rule.DataProperty, displayValue));
                    }
                }
            }
        }
    }
}
