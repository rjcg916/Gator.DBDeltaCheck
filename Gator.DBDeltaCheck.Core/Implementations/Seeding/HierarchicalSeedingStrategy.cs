using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;

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
        // 1. Read configuration from the parameters.
        var dataFilePath = parameters["dataFile"]?.Value<string>()
                           ?? throw new ArgumentException(
                               "Invalid config for HierarchicalSeedingStrategy. 'dataFile' is required.");

        var allowIdentityInsert = parameters["allowIdentityInsert"]?.Value<bool>() ?? false;

        // 2. Load the hierarchical data from the specified file.
        var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();
        var absoluteDataFilePath = Path.Combine(basePath, dataFilePath);

        if (!File.Exists(absoluteDataFilePath))
            throw new FileNotFoundException(
                $"The specified hierarchical data file was not found: {absoluteDataFilePath}");

        var fileContent = await File.ReadAllTextAsync(absoluteDataFilePath);
        var seedData = JObject.Parse(fileContent);

        // 3. Get the root table and the data array from the loaded file's content.
        var rootTable = seedData["rootTable"]?.Value<string>()
                        ?? throw new ArgumentException(
                            $"The hierarchical data file '{dataFilePath}' is missing the 'rootTable' property.");

        var data = seedData["data"]
                   ?? throw new ArgumentException(
                       $"The hierarchical data file '{dataFilePath}' is missing the 'data' property.");

        // 4. Start the recursive process.
        await ProcessToken(rootTable, data, new Dictionary<string, object>(), allowIdentityInsert);

        // 5. Process the "Outputs" section
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

    private async Task ProcessToken(string tableName, JToken token, Dictionary<string, object> parentKeys,
        bool allowIdentityInsert)
    {
        if (token is JArray array)
            foreach (var record in array.Children<JObject>())
                await ProcessRecord(tableName, record, parentKeys, allowIdentityInsert);
        else if (token is JObject record) await ProcessRecord(tableName, record, parentKeys, allowIdentityInsert);
    }

    private async Task ProcessRecord(string tableName, JObject record, Dictionary<string, object> parentKeys,
        bool allowIdentityInsert)
    {
        var recordData = record.ToObject<Dictionary<string, JToken>>()!;

        var childNodes = new Dictionary<string, JToken>();
        var schemaRelations = await _schemaService.GetChildTablesAsync(tableName);
        foreach (var relation in schemaRelations)
            if (recordData.TryGetValue(relation.ChildCollectionName, out var childData))
            {
                childNodes.Add(relation.ChildTableName, childData);
                recordData.Remove(relation.ChildCollectionName);
            }

        var finalRecordData = await PrepareRecordData(tableName, recordData, parentKeys);

        var primaryKeyName = await _schemaService.GetPrimaryKeyColumnNameAsync(tableName);
        var primaryKeyType = await _schemaService.GetPrimaryKeyTypeAsync(tableName);

        var methodInfo = typeof(IDatabaseRepository).GetMethod(nameof(IDatabaseRepository.InsertRecordAndGetIdAsync));
        var genericMethod = methodInfo.MakeGenericMethod(primaryKeyType);

        // Pass the allowIdentityInsert flag to the repository.
        var task = (Task)genericMethod.Invoke(_repository,
            new object[] { tableName, finalRecordData, primaryKeyName, allowIdentityInsert });
        await task;

        var primaryKeyValue = ((dynamic)task).Result;

        foreach (var childNode in childNodes)
        {
            var childTableName = childNode.Key;
            var childData = childNode.Value;
            var foreignKeyForParent = await _schemaService.GetForeignKeyColumnNameAsync(childTableName, tableName);

            var newParentKeys = new Dictionary<string, object> { { foreignKeyForParent, primaryKeyValue } };
            await ProcessToken(childTableName, childData, newParentKeys, allowIdentityInsert);
        }
    }

    private async Task<Dictionary<string, object>> PrepareRecordData(
        string tableName,
        Dictionary<string, JToken> recordData,
        Dictionary<string, object> parentKeys)
    {
        var finalData = new Dictionary<string, object>();

        foreach (var property in recordData)
            if (property.Value is JObject lookupObject &&
                lookupObject.TryGetValue("_lookupTable", out var lookupTableToken))
            {
                var lookupTableName = lookupTableToken.Value<string>();
                var valueColumn = lookupObject["_lookupValueColumn"].Value<string>();
                var displayValue = lookupObject["_lookupDisplayValue"].ToObject<object>();

                var primaryKeyOfLookupTable = await _schemaService.GetPrimaryKeyColumnNameAsync(lookupTableName);
                var sql =
                    $"SELECT {primaryKeyOfLookupTable} FROM {lookupTableName} WHERE {valueColumn} = @displayValue";
                var foreignKeyId = await _repository.ExecuteScalarAsync<object>(sql, new { displayValue });

                if (foreignKeyId == null)
                    throw new InvalidOperationException(
                        $"Lookup failed: Could not find a record in table '{lookupTableName}' where column '{valueColumn}' equals '{displayValue}'.");

                var targetForeignKeyColumn =
                    await _schemaService.GetForeignKeyColumnNameAsync(tableName, lookupTableName);
                finalData[targetForeignKeyColumn] = foreignKeyId;
            }
            else
            {
                finalData[property.Key] = property.Value.ToObject<object>();
            }

        foreach (var parentKey in parentKeys) finalData[parentKey.Key] = parentKey.Value;

        return finalData;
    }
}

internal class OutputInstruction
{
    [JsonProperty("VariableName")] public string VariableName { get; set; }

    [JsonProperty("Source")] public OutputSource Source { get; set; }
}

internal class OutputSource
{
    [JsonProperty("FromTable")] public string FromTable { get; set; }

    [JsonProperty("SelectColumn")] public string SelectColumn { get; set; }

    [JsonProperty("OrderByColumn")] public string OrderByColumn { get; set; }

    [JsonProperty("OrderDirection")] public string? OrderDirection { get; set; }
}