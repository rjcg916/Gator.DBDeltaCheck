using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;

    public class HierarchicalSeedingStrategy(
        IDatabaseRepository repository,
        IDbSchemaService schemaService,
        IDataMapper dataMapper)
        : BaseSetupStrategy(repository)
    {
        public override string StrategyName => "HierarchicalSeed";

        public override async Task ExecuteAsync(JObject parameters, Dictionary<string, object> testContext, DataMap? dataMap)
        {
            var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();
            var dataFilePath = parameters["dataFile"]?.Value<string>()
                ?? throw new System.ArgumentException("Config is missing 'dataFile'.");

            var allowIdentityInsert = parameters["allowIdentityInsert"]?.Value<bool>() ?? false;

            var absoluteDataFilePath = Path.Combine(basePath, dataFilePath);
            if (!File.Exists(absoluteDataFilePath))
                throw new FileNotFoundException($"The data file was not found: {absoluteDataFilePath}");

            var fileContent = await File.ReadAllTextAsync(absoluteDataFilePath);
            var seedData = JObject.Parse(fileContent);

            var rootTable = seedData["rootTable"]?.Value<string>()
                ?? throw new System.ArgumentException($"Data file '{dataFilePath}' is missing 'rootTable'.");

            var data = seedData["data"]
                ?? throw new System.ArgumentException($"Data file '{dataFilePath}' is missing 'data'.");

            await ProcessToken(rootTable, data, new Dictionary<string, object>(), allowIdentityInsert, dataMap);

            if (parameters.TryGetValue("Outputs", out var outputsToken) && outputsToken is JArray outputsArray)
            {
                await base.ProcessOutputs(outputsArray, testContext);
            }
        }

        private async Task ProcessToken(string tableName, JToken token, Dictionary<string, object> parentKeys, bool allowIdentityInsert, DataMap? dataMap)
        {
            switch (token)
            {
                case JArray array:
                {
                    foreach (var record in array.Children<JObject>())
                    {
                        await ProcessRecord(tableName, record, parentKeys, allowIdentityInsert, dataMap);
                    }

                    break;
                }
                case JObject record:
                    await ProcessRecord(tableName, record, parentKeys, allowIdentityInsert, dataMap);
                    break;
            }
        }


        private async Task ProcessRecord(string tableName, JObject record, Dictionary<string, object> parentKeys, bool allowIdentityInsert, DataMap? dataMap)
        {

            // Filter out any "commented" properties from the raw record.
            var activeProperties = record.Properties()
                .Where(p => !p.Name.Trim().StartsWith("//"));

            var recordForInsertion = new JObject(activeProperties);
            var childNodes = new Dictionary<string, (JToken childData, string navigationName)>();
            var schemaRelations = await schemaService.GetChildTablesAsync(tableName);
  
            foreach (var relation in schemaRelations)
            {
                var property = recordForInsertion.Property(relation.ChildCollectionName, System.StringComparison.OrdinalIgnoreCase);
                if (property != null)
                {
                    childNodes.Add(relation.ChildTableName, (property.Value, relation.ChildCollectionName));
                    property.Remove();
                }
            }

            // Use the DataMapper to resolve all lookups.
            var resolvedJson = await dataMapper.ResolveToDbState(recordForInsertion.ToString(), dataMap, tableName, null);
            var finalData = JsonConvert.DeserializeObject<Dictionary<string, object>>(resolvedJson);

            // Add parent keys.
            foreach (var parentKey in parentKeys)
            {
                finalData[parentKey.Key] = parentKey.Value;
            }

            var primaryKeyName = await schemaService.GetPrimaryKeyColumnNameAsync(tableName);
            var primaryKeyType = await schemaService.GetPrimaryKeyTypeAsync(tableName);

            var methodInfo = typeof(IDatabaseRepository).GetMethod(nameof(IDatabaseRepository.InsertRecordAndGetIdAsync));
            var genericMethod = methodInfo.MakeGenericMethod(primaryKeyType);

            var task = (Task)genericMethod.Invoke(Repository, new object[] { tableName, finalData, primaryKeyName, allowIdentityInsert });
            await task;

            var primaryKeyValue = ((dynamic)task).Result;

            foreach (var (childTableName, (childData, navigationName)) in childNodes)
            {
                var foreignKeyForParent = await schemaService.GetForeignKeyColumnNameAsync(childTableName, navigationName );
                var newParentKeys = new Dictionary<string, object> { { foreignKeyForParent, primaryKeyValue } };
                await ProcessToken(childTableName, childData, newParentKeys, allowIdentityInsert, dataMap);
            }
        }
    }

