using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding
{
    public class HierarchicalSeedingStrategy : ISetupStrategy
    {
        public string StrategyName => "HierarchicalSeed";

        // The strategy now holds all the tools it needs as private fields.
        private readonly IDatabaseRepository _repository;
        private readonly IDbSchemaService _schemaService;

        /// <summary>
        /// All dependencies are now injected via the constructor by the DI container.
        /// </summary>
        public HierarchicalSeedingStrategy(IDatabaseRepository repository, IDbSchemaService schemaService)
        {
            _repository = repository;
            _schemaService = schemaService;
        }

        /// <summary>
        /// The ExecuteAsync method is now clean and only accepts the parameters from the JSON file.
        /// </summary>
        public async Task ExecuteAsync(JObject parameters)
        {
            var rootTable = parameters["rootTable"]?.Value<string>();
            var data = parameters["data"];

            if (data == null || string.IsNullOrWhiteSpace(rootTable))
            {
                throw new ArgumentException("Invalid config for HierarchicalSeedingStrategy. 'rootTable' and 'data' are required.");
            }

            // Start the recursive process using the injected dependencies.
            await ProcessToken(rootTable, data, new Dictionary<string, object>());
        }

        /// <summary>
        /// Processes a JToken, handling whether it's a single object or an array of objects.
        /// </summary>
        private async Task ProcessToken(string tableName, JToken token, Dictionary<string, object> parentKeys)
        {
            if (token is JArray array)
            {
                foreach (var record in array.Children<JObject>())
                {
                    await ProcessRecord(tableName, record, parentKeys);
                }
            }
            else if (token is JObject record)
            {
                await ProcessRecord(tableName, record, parentKeys);
            }
        }

        /// <summary>
        /// Processes a single JObject record, inserts it, and then recursively processes its children.
        /// </summary>
        private async Task ProcessRecord(string tableName, JObject record, Dictionary<string, object> parentKeys)
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
                    recordData.Remove(relation.ChildCollectionName);
                }
            }

            // 2. Prepare the final data for insertion, resolving lookups and adding parent keys.
            var finalRecordData = await PrepareRecordData(tableName, recordData, parentKeys);

            // 3. Get all necessary primary key info from the schema service.
            var primaryKeyName = await _schemaService.GetPrimaryKeyColumnNameAsync(tableName);
            var primaryKeyType = await _schemaService.GetPrimaryKeyTypeAsync(tableName);

            // 4. Use reflection to dynamically call the generic InsertRecordAndGetIdAsync<T> method.
            var methodInfo = typeof(IDatabaseRepository).GetMethod(nameof(IDatabaseRepository.InsertRecordAndGetIdAsync));
            var genericMethod = methodInfo.MakeGenericMethod(primaryKeyType);

            // The result of Invoke is a Task, which we must await.
            var task = (Task)genericMethod.Invoke(_repository, new object[] { tableName, finalRecordData, primaryKeyName });
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
                await ProcessToken(childTableName, childData, newParentKeys);
            }
        }

        /// <summary>
        /// Resolves lookups and adds parent foreign keys to create the final data dictionary for insertion.
        /// </summary>
        private async Task<Dictionary<string, object>> PrepareRecordData(
            string tableName,
            Dictionary<string, JToken> recordData,
            Dictionary<string, object> parentKeys)
        {
            var finalData = new Dictionary<string, object>();

            foreach (var property in recordData)
            {
                if (property.Value is JObject lookupObject && lookupObject.TryGetValue("_lookupTable", out var lookupTableToken))
                {
                    var lookupTableName = lookupTableToken.Value<string>();
                    var valueColumn = lookupObject["_lookupValueColumn"].Value<string>();
                    var displayValue = lookupObject["_lookupDisplayValue"].ToObject<object>();

                    var primaryKeyOfLookupTable = await _schemaService.GetPrimaryKeyColumnNameAsync(lookupTableName);
                    var sql = $"SELECT {primaryKeyOfLookupTable} FROM {lookupTableName} WHERE {valueColumn} = @displayValue";
                    var foreignKeyId = await _repository.ExecuteScalarAsync<object>(sql, new { displayValue });

                    if (foreignKeyId == null)
                    {
                        throw new InvalidOperationException($"Lookup failed: Could not find a record in table '{lookupTableName}' where column '{valueColumn}' equals '{displayValue}'.");
                    }

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
