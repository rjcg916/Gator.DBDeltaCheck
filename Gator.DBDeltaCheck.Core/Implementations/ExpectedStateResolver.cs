using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class ExpectedStateResolver : IExpectedStateResolver
{
    private readonly IDatabaseRepository _repository;
    private readonly IDbSchemaService _schemaService;

    public ExpectedStateResolver(IDatabaseRepository repository, IDbSchemaService schemaService)
    {
        _repository = repository;
        _schemaService = schemaService;
    }

    public async Task<JToken> Resolve(JToken expectedStateToken, DataMap dataMap, string tableName)
    {
        // Clone the token so we don't modify the original
        var resolvedToken = expectedStateToken.DeepClone();

        if (resolvedToken is JArray array)
        {
            foreach (var item in array.Children<JObject>())
            {
                await ResolveRecord(item, dataMap);
            }
        }
        else if (resolvedToken is JObject obj)
        {
            await ResolveRecord(obj, dataMap);
        }

        return resolvedToken;
    }

    private async Task ResolveRecord(JObject record, DataMap dataMap)
    {
        // We need to know which table this record belongs to.
        // This assumes the assertion knows the table, which it does.
        var tableName = record["_tableName_placeholder_"]?.Value<string>(); // We'll inject this
        record.Remove("_tableName_placeholder_");

        if (string.IsNullOrEmpty(tableName)) return;

        var tableMap = dataMap.Tables.FirstOrDefault(t => t.Name.Equals(tableName, System.StringComparison.OrdinalIgnoreCase));
        if (tableMap == null) return;

        var propertiesToResolve = new Dictionary<string, JProperty>();
        foreach (var prop in record.Properties())
        {
            propertiesToResolve.Add(prop.Name, prop);
        }

        foreach (var rule in tableMap.Lookups)
        {
            if (propertiesToResolve.TryGetValue(rule.DataProperty, out var property))
            {
                var displayValue = property.Value.ToObject<object>();
                var pkColumn = await _schemaService.GetPrimaryKeyColumnNameAsync(rule.LookupTable);
                var sql = $"SELECT {pkColumn} FROM {rule.LookupTable} WHERE {rule.LookupValueColumn} = @displayValue";
                var resolvedId = await _repository.ExecuteScalarAsync<object>(sql, new { displayValue });

                if (resolvedId == null)
                    throw new System.InvalidOperationException($"Expected state lookup failed: Could not find record in '{rule.LookupTable}' where '{rule.LookupValueColumn}' is '{displayValue}'.");

                // Transform the JSON: Rename the property and replace the value
                var fkColumnName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
                property.Replace(new JProperty(fkColumnName, resolvedId));
            }
        }
    }
}

