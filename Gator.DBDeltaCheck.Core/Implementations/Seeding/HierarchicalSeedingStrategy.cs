using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;


public class HierarchicalSeedingStrategy : ISetupStrategy
{
    public string StrategyName => "HierarchicalSeed";

    private readonly IDbSchemaService _schemaService;

    public HierarchicalSeedingStrategy(IDbSchemaService schemaService)
    {
        _schemaService = schemaService;
    }

    public async Task ExecuteAsync(IDatabaseRepository repository, JObject config)
    {
        var rootTable = config["rootTable"]?.Value<string>();
        var data = config["data"];

        if (data == null || string.IsNullOrWhiteSpace(rootTable))
        {
            throw new ArgumentException("Invalid config for HierarchicalSeedingStrategy. 'rootTable' and 'data' are required.");
        }

        // Start the recursive process
        await ProcessToken(repository, rootTable, data, new Dictionary<string, object>());
    }

    /// <summary>
    /// Processes a JToken, handling whether it's a single object or an array of objects.
    /// </summary>
    private async Task ProcessToken(IDatabaseRepository repository, string tableName, JToken token, Dictionary<string, object> parentKeys)
    {
        if (token is JArray array)
        {
            foreach (var record in array.Children<JObject>())
            {
                await ProcessRecord(repository, tableName, record, parentKeys);
            }
        }
        else if (token is JObject record)
        {
            await ProcessRecord(repository, tableName, record, parentKeys);
        }
    }

    /// <summary>
    /// Processes a single JObject record, inserts it, and then recursively processes its children.
    /// </summary>
    private async Task ProcessRecord(IDatabaseRepository repository, string tableName, JObject record, Dictionary<string, object> parentKeys)
    {
        var recordData = record.ToObject<Dictionary<string, JToken>>()!;

        // 1. First, separate child data from the current record's data.
        var childNodes = new Dictionary<string, JToken>();
        var schemaRelations = await _schemaService.GetChildTablesAsync(tableName);
        foreach (var relation in schemaRelations)
        {
            if (recordData.TryGetValue(relation.ChildCollectionName, out var childData))
            {
                childNodes.Add(relation.ChildTableName, childData);
                recordData.Remove(relation.ChildCollectionName); // Remove from data to be inserted
            }
        }

        // 2. Prepare the final data for insertion, resolving lookups and adding parent keys.
        var finalRecordData = await PrepareRecordData(repository, tableName, recordData, parentKeys);

  
        // 3. Get all necessary primary key info from the schema service.
        var primaryKeyName = await _schemaService.GetPrimaryKeyColumnNameAsync(tableName);
        var primaryKeyType = await _schemaService.GetPrimaryKeyTypeAsync(tableName); // Get the Type

        // 4. Use reflection to dynamically call the generic InsertRecordAndGetIdAsync<T> method.
        var methodInfo = typeof(IDatabaseRepository).GetMethod(nameof(IDatabaseRepository.InsertRecordAndGetIdAsync));
        var genericMethod = methodInfo.MakeGenericMethod(primaryKeyType);

        // The result of Invoke is a Task, which we must await.
        var task = (Task)genericMethod.Invoke(repository, new object[] { tableName, finalRecordData, primaryKeyName });
        await task;

        // The result of the awaited task is the primary key value.
        var primaryKeyValue = ((dynamic)task).Result;
        

        // 5. Recursively process all child nodes, passing the new primary key down.
        foreach (var childNode in childNodes)
        {
            var childTableName = childNode.Key;
            var childData = childNode.Value;
            var foreignKeyForParent = await _schemaService.GetForeignKeyColumnNameAsync(childTableName, tableName);

            var newParentKeys = new Dictionary<string, object> { { foreignKeyForParent, primaryKeyValue } };
            await ProcessToken(repository, childTableName, childData, newParentKeys);
        }
    }

    /// <summary>
    /// Resolves lookups and adds parent foreign keys to create the final data dictionary for insertion.
    /// </summary>
    private async Task<Dictionary<string, object>> PrepareRecordData(IDatabaseRepository repository, string tableName, Dictionary<string, JToken> recordData, Dictionary<string, object> parentKeys)
    {
        var finalData = new Dictionary<string, object>();

        // Process each property in the JSON record
        foreach (var property in recordData)
        {
            // Check for our special "_lookup" convention
            if (property.Value is JObject lookupObject && lookupObject.TryGetValue("_lookupTable", out var lookupTableToken))
            {
                var lookupTableName = lookupTableToken.Value<string>();
                var valueColumn = lookupObject["_lookupValueColumn"].Value<string>();
                var displayValue = lookupObject["_lookupDisplayValue"].Value<object>(); // Use object for flexibility
                var targetForeignKey = await _schemaService.GetForeignKeyColumnNameAsync(tableName, lookupTableName);

                var foreignKeyId = await repository.GetLookupIdAsync(lookupTableName, valueColumn, displayValue);
                finalData[targetForeignKey] = foreignKeyId;
            }
            else
            {
                // It's a regular value, just add it
                finalData[property.Key] = property.Value.ToObject<object>();
            }
        }

        // Add foreign keys from the parent record
        foreach (var parentKey in parentKeys)
        {
            finalData[parentKey.Key] = parentKey.Value;
        }

        return finalData;
    }
}