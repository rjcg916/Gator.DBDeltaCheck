using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;

public class HierarchicalSeedingStrategy : ISetupStrategy
{
    private readonly IDatabaseRepository _repository;
    private readonly IDbSchemaService _schemaService;
    private readonly IDataMapper _dataMapper;

    public HierarchicalSeedingStrategy(IDatabaseRepository repository, IDbSchemaService schemaService, IDataMapper dataMapper)
    {
        _repository = repository;
        _schemaService = schemaService;
        _dataMapper = dataMapper;
    }

    public string StrategyName => "HierarchicalSeed";

    public async Task Setup(JObject parameters, Dictionary<string, object> testContext, DataMap? dataMap)
    {

        // 1. Get all parameters from the JSON.
        var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();

        var dataFilePath = parameters["dataFile"]?.Value<string>()
            ?? throw new ArgumentException("Config is missing 'dataFile'.");

        var allowIdentityInsert = parameters["allowIdentityInsert"]?.Value<bool>() ?? false;

        // 2. Resolve the absolute path and check if the file exists.
        var absoluteDataFilePath = Path.Combine(basePath, dataFilePath);
        if (!File.Exists(absoluteDataFilePath))
            throw new FileNotFoundException($"The data file was not found: {absoluteDataFilePath}");

        var fileContent = await File.ReadAllTextAsync(absoluteDataFilePath);
        var seedData = JObject.Parse(fileContent);

        // 3. Validate the seed data structure.
        var rootTable = seedData["rootTable"]?.Value<string>()
            ?? throw new ArgumentException($"Data file '{dataFilePath}' is missing 'rootTable'.");

        var data = seedData["data"]
            ?? throw new ArgumentException($"Data file '{dataFilePath}' is missing 'data'.");

        // 4. Process the root table and its records.
        await ProcessToken(rootTable, data, new Dictionary<string, object>(), allowIdentityInsert, dataMap);

        // 5. If there are any output instructions, process them.
        if (parameters.TryGetValue("Outputs", out var outputsToken) && outputsToken is JArray outputsArray)
        {
            await ProcessOutputs(outputsArray, testContext);
        }
    }

    private async Task ProcessToken(string tableName, JToken token, Dictionary<string, object> parentKeys, bool allowIdentityInsert, DataMap? dataMap)
    {
        if (token is JArray array)
        {
            foreach (var record in array.Children<JObject>())
            {
                await ProcessRecord(tableName, record, parentKeys, allowIdentityInsert, dataMap);
            }
        }
        else if (token is JObject record)
        {
            await ProcessRecord(tableName, record, parentKeys, allowIdentityInsert, dataMap);
        }
    }
    private async Task ProcessRecord(string tableName, JObject record, Dictionary<string, object> parentKeys, bool allowIdentityInsert, DataMap? dataMap)
    {
        var recordData = record.ToObject<Dictionary<string, JToken>>()!;

        var childNodes = new Dictionary<string, JToken>();
        var schemaRelations = await _schemaService.GetChildTablesAsync(tableName);
        foreach (var relation in schemaRelations)
        {
            if (recordData.TryGetValue(relation.ChildCollectionName, out var childData))
            {
                childNodes.Add(relation.ChildTableName, childData);
                recordData.Remove(relation.ChildCollectionName);
            }
        }

        var finalRecordData = await PrepareRecordData(tableName, recordData, parentKeys, dataMap);

        var primaryKeyName = await _schemaService.GetPrimaryKeyColumnNameAsync(tableName);
        var primaryKeyType = await _schemaService.GetPrimaryKeyTypeAsync(tableName);

        var methodInfo = typeof(IDatabaseRepository).GetMethod(nameof(IDatabaseRepository.InsertRecordAndGetIdAsync));
        var genericMethod = methodInfo.MakeGenericMethod(primaryKeyType);

        var task = (Task)genericMethod.Invoke(_repository, new object[] { tableName, finalRecordData, primaryKeyName, allowIdentityInsert });
        await task;

        var primaryKeyValue = ((dynamic)task).Result;

        foreach (var childNode in childNodes)
        {
            var childTableName = childNode.Key;
            var childData = childNode.Value;
            var foreignKeyForParent = await _schemaService.GetForeignKeyColumnNameAsync(childTableName, tableName);
            var newParentKeys = new Dictionary<string, object> { { foreignKeyForParent, primaryKeyValue } };
            await ProcessToken(childTableName, childData, newParentKeys, allowIdentityInsert, dataMap);
        }
    }

    private async Task<Dictionary<string, object>> PrepareRecordData(
        string tableName,
        Dictionary<string, JToken> recordData,
        Dictionary<string, object> parentKeys,
        DataMap? dataMap)
    {
        // 1. Convert the current record's data into a temporary JObject.
        var tempRecord = JObject.FromObject(recordData);

        // 2. Use the DataMapper to resolve all lookups (both mapped and inline).
        var resolvedJson = await _dataMapper.ResolveToDbState(tempRecord.ToString(), dataMap, tableName);

        // 3. Convert the resolved JSON back into a dictionary.
        var finalData = JsonConvert.DeserializeObject<Dictionary<string, object>>(resolvedJson);

        // 4. Add the parent keys
        foreach (var parentKey in parentKeys)
        {
            finalData[parentKey.Key] = parentKey.Value;
        }

        return finalData;
    }
    private async Task ProcessOutputs(JArray outputsArray, Dictionary<string, object> testContext)
    {
        var outputInstructions = outputsArray.ToObject<List<OutputInstruction>>();
        if (outputInstructions == null) return;

        foreach (var instruction in outputInstructions)
        {
            var source = instruction.Source;
            var sql = $"SELECT TOP 1 {source.SelectColumn} FROM {source.FromTable} ORDER BY {source.OrderByColumn} {source.OrderDirection ?? "DESC"}";
            var outputValue = await _repository.ExecuteScalarAsync<object>(sql);
            if (outputValue != null)
            {
                testContext[instruction.VariableName] = outputValue;
            }
        }
    }
}