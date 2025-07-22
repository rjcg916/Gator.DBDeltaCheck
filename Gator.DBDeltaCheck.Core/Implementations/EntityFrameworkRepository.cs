using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

public class HierarchicalSeedingStrategy : ISetupStrategy
{
    private readonly IDbSchemaService _schemaService;

    public HierarchicalSeedingStrategy(IDbSchemaService schemaService)
    {
        _schemaService = schemaService;
    }

    public async Task ExecuteAsync(IDatabaseRepository repository, JObject config)
    {
        var instruction = config.ToObject<HierarchicalSeedInstruction>();
        if (instruction == null)
        {
            throw new InvalidOperationException("Invalid config for HierarchicalSeedingStrategy.");
        }

        // 1. Discover schema and determine insertion order via topological sort
        var tableDependencies = _schemaService.GetTableDependencies();
        var sortedTables = TopologicalSort(tableDependencies); // Custom implementation of topological sort

        // 2. Process the data recursively
        await ProcessNode(repository, instruction.RootTable, instruction.Data, instruction.Lookups, null);
    }

    public Task ExecuteAsync(IDatabaseOperations repository, JObject config)
    {
        throw new NotImplementedException();
    }

    private async Task ProcessNode(IDatabaseRepository repository, string tableName, JToken data,
                                   List<LookupInstruction> lookups, Dictionary<string, object> parentKeys)
    {
        var records = data is JArray ? data.Children<JObject>() : new { (JObject)data };

        foreach (var record in records)
        {
            // 3. Resolve lookups for the current record
            foreach (var lookup in lookups.Where(l => record.ContainsKey(l.SourceField)))
            {
                var lookupValue = record.ToString();
                var foreignKey = await repository.GetLookupIdAsync(
                    lookup.LookupTable,
                    lookup.LookupColumn,
                    lookupValue,
                    lookup.LookupResultColumn);

                record.Remove(lookup.SourceField);
                record.Add(lookup.TargetForeignKey, JToken.FromObject(foreignKey));
            }

            // 4. Add parent foreign keys if applicable
            if (parentKeys != null)
            {
                foreach (var key in parentKeys)
                {
                    record.Add(key.Key, JToken.FromObject(key.Value));
                }
            }

            // 5. Separate child data from the current record's data
            var childNodes = new Dictionary<string, JToken>();
            var schemaRelations = _schemaService.GetChildTables(tableName);
            foreach (var relation in schemaRelations)
            {
                if (record.TryGetValue(relation.ChildCollectionName, out var childData))
                {
                    childNodes.Add(relation.ChildTableName, childData);
                    record.Remove(relation.ChildCollectionName);
                }
            }

            // 6. Insert the current record and get its primary key
            var primaryKey = await repository.InsertAndGetIdAsync(tableName, record.ToString());

            // 7. Recursively process child nodes
            foreach (var childNode in childNodes)
            {
                var childTableName = childNode.Key;
                var childData = childNode.Value;
                var newParentKeys = new Dictionary<string, object>
                {
                    { _schemaService.GetForeignKeyColumnName(childTableName, tableName), primaryKey }
                };
                await ProcessNode(repository, childTableName, childData, lookups, newParentKeys);
            }
        }
    }
}