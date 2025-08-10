using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations
{
    public class ActualStateMapper : IActualStateMapper
    {
        private readonly IDatabaseRepository _repository;
        private readonly IDbSchemaService _schemaService;

        public ActualStateMapper(IDatabaseRepository repository, IDbSchemaService schemaService)
        {
            _repository = repository;
            _schemaService = schemaService;
        }

        public async Task<string> Map(string rawActualStateJson, DataMap dataMap, string tableName)
        {
            var token = JToken.Parse(rawActualStateJson).DeepClone();
            var tableMap = dataMap.Tables.FirstOrDefault(t => t.Name.Equals(tableName, System.StringComparison.OrdinalIgnoreCase));

            if (tableMap == null) return rawActualStateJson;

            if (token is JArray array)
            {
                foreach (var item in array.Children<JObject>())
                {
                    await MapRecord(item, tableName, tableMap);
                }
            }
            else if (token is JObject obj)
            {
                await MapRecord(obj, tableName, tableMap);
            }

            return token.ToString();
        }

        private async Task MapRecord(JObject record, string tableName, TableMap tableMap)
        {
            foreach (var rule in tableMap.Lookups)
            {
                var fkColumnName = await _schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
                var property = record.Property(fkColumnName, System.StringComparison.OrdinalIgnoreCase);

                if (property != null)
                {
                    var idValue = property.Value.ToObject<object>();
                    var sql = $"SELECT {rule.LookupValueColumn} FROM {rule.LookupTable} WHERE {await _schemaService.GetPrimaryKeyColumnNameAsync(rule.LookupTable)} = @idValue";
                    var displayValue = await _repository.ExecuteScalarAsync<object>(sql, new { idValue });

                    if (displayValue != null)
                    {
                        // Transform the JSON: Rename the property and replace the ID with the friendly value.
                        property.Replace(new JProperty(rule.DataProperty, displayValue));
                    }
                }
            }
        }
    }
}
