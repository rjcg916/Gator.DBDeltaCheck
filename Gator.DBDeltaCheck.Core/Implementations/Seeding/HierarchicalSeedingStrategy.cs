using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding
{
    public class HierarchicalSeedingStrategy : ISetupStrategy
    {
        private readonly IDatabaseRepository _repository;
        private readonly IDbSchemaService _schemaService;

        public HierarchicalSeedingStrategy(IDatabaseRepository repository, IDbSchemaService schemaService)
        {
            _repository = repository;
            _schemaService = schemaService;
        }

        public string StrategyName => "HierarchicalSeed";

        public async Task ExecuteAsync(JObject parameters, Dictionary<string, object> testContext)
        {

            var dataFilePath = parameters["dataFile"]?.Value<string>()
                ?? throw new ArgumentException("Config is missing 'dataFile'.");
            
            var dataMapFilePath = parameters["dataMapFile"]?.Value<string>();

            var allowIdentityInsert = parameters["allowIdentityInsert"]?.Value<bool>() ?? false;
            var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();

            var lookupMap = new Dictionary<string, TableMap>();


            if (!string.IsNullOrEmpty(dataMapFilePath))
            {
                var absoluteDataMapPath = Path.Combine(basePath, dataMapFilePath);
                if (!File.Exists(absoluteDataMapPath))
                    throw new FileNotFoundException($"The data map file was not found: {absoluteDataMapPath}");

                var mapContent = await File.ReadAllTextAsync(absoluteDataMapPath);
                var dataMap = JsonConvert.DeserializeObject<DataMap>(mapContent);
                lookupMap = dataMap.Tables.ToDictionary(t => t.Name, t => t);
            }

            var absoluteDataFilePath = Path.Combine(basePath, dataFilePath);
            if (!File.Exists(absoluteDataFilePath))
                throw new FileNotFoundException($"The data file was not found: {absoluteDataFilePath}");

            var fileContent = await File.ReadAllTextAsync(absoluteDataFilePath);
            var seedData = JObject.Parse(fileContent);

            var rootTable = seedData["rootTable"]?.Value<string>()
                ?? throw new ArgumentException($"Data file '{dataFilePath}' is missing 'rootTable'.");

            var data = seedData["data"]
                ?? throw new ArgumentException($"Data file '{dataFilePath}' is missing 'data'.");


            await ProcessToken(rootTable, data, new Dictionary<string, object>(), allowIdentityInsert, lookupMap);


            if (parameters.TryGetValue("Outputs", out var outputsToken) && outputsToken is JArray outputsArray)
            {
                var outputInstructions = outputsArray.ToObject<List<OutputInstruction>>();
                foreach (var instruction in outputInstructions)
                {
                    var source = instruction.Source;
                    var sql =
                        $"SELECT TOP 1 {source.SelectColumn} FROM {source.FromTable} ORDER BY {source.OrderByColumn} {source.OrderDirection ?? "DESC"}";

                    var outputValue = await _repository.ExecuteScalarAsync<object>(sql);

                    if (outputValue != null)
                        // Write the captured value to the test context dictionary.
                        testContext[instruction.VariableName] = outputValue;
                }
            }
        }

        // The private recursive methods now pass the 'dataMap' dictionary down the call stack.
        private async Task ProcessToken(string tableName, JToken token, Dictionary<string, object> parentKeys, bool allowIdentityInsert, Dictionary<string, TableMap> dataMap) { /* ... unchanged ... */ }
        private async Task ProcessRecord(string tableName, JObject record, Dictionary<string, object> parentKeys, bool allowIdentityInsert, Dictionary<string, TableMap> dataMap) { /* ... unchanged ... */ }


        private async Task<Dictionary<string, object>> PrepareRecordData(
            string tableName,
            Dictionary<string, JToken> recordData,
            Dictionary<string, object> parentKeys,
            Dictionary<string, TableMap> dataMap)
        {
            var finalData = new Dictionary<string, object>();

            dataMap.TryGetValue(tableName, out var tableMap);
            var lookupRules = tableMap?.Lookups.ToDictionary(r => r.DataProperty, r => r)
                              ?? new Dictionary<string, LookupRule>();

            foreach (var property in recordData)
            {
                // check mapped lookups first
                if (lookupRules.TryGetValue(property.Key, out var rule))
                {
                    var displayValue = property.Value.ToObject<object>();
                    var primaryKeyOfLookupTable = await _schemaService.GetPrimaryKeyColumnNameAsync(rule.LookupTable);

                    var sql = $"SELECT {primaryKeyOfLookupTable} FROM {rule.LookupTable} WHERE {rule.LookupValueColumn} = @displayValue";
                    var foreignKeyId = await _repository.ExecuteScalarAsync<object>(sql, new { displayValue });

                    if (foreignKeyId == null)
                        throw new InvalidOperationException($"Lookup failed via map: Could not find record in '{rule.LookupTable}' where '{rule.LookupValueColumn}' is '{displayValue}'.");

                    var targetForeignKeyColumn = await _schemaService.GetForeignKeyColumnNameAsync(tableName, rule.LookupTable);
                    finalData[targetForeignKeyColumn] = foreignKeyId;
                }
                // use local lookup
                else if (property.Value is JObject lookupObject && lookupObject.TryGetValue("_lookupTable", out var lookupTableToken))
                {
                    var lookupTableName = lookupTableToken.Value<string>();
                    var valueColumn = lookupObject["_lookupValueColumn"].Value<string>();
                    var displayValue = lookupObject["_lookupDisplayValue"].ToObject<object>();

                    var primaryKeyOfLookupTable = await _schemaService.GetPrimaryKeyColumnNameAsync(lookupTableName);
                    var sql = $"SELECT {primaryKeyOfLookupTable} FROM {lookupTableName} WHERE {valueColumn} = @displayValue";
                    var foreignKeyId = await _repository.ExecuteScalarAsync<object>(sql, new { displayValue });

                    if (foreignKeyId == null)
                        throw new InvalidOperationException($"Lookup failed via inline: Could not find record in '{lookupTableName}' where '{valueColumn}' is '{displayValue}'.");

                    var targetForeignKeyColumn = await _schemaService.GetForeignKeyColumnNameAsync(tableName, lookupTableName);
                    finalData[targetForeignKeyColumn] = foreignKeyId;
                }
                else
                {
                    finalData[property.Key] = property.Value.ToObject<object>();
                }
            }

            foreach (var parentKey in parentKeys)
            {
                finalData[parentKey.Key] = parentKey.Value;
            }

            return finalData;
        }
    }
}
