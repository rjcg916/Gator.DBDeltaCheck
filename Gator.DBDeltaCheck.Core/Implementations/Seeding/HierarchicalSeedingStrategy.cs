using Gator.DBDeltaCheck.Core.Abstractions;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;
public class HierarchicalSeedingStrategy : ISetupStrategy
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDatabaseRepository _dbRepository;
    public string StrategyName => "HierarchicalSeed";

    public HierarchicalSeedingStrategy(IDbSchemaService schemaService, IDatabaseRepository dbRepository)
    {
        _schemaService = schemaService;
        _dbRepository = dbRepository;
    }

    public async Task ExecuteAsync(object parameters)
    {
        // 1. Deserialize parameters into a structure representing the hierarchical JSON.
        // 2. Start with the root table.
        // 3. For each row in the root:
        //    a. Inspect its columns. For any column that is a foreign key (use _schemaService):
        //       i. Get the lookup table name and value from the JSON.
        //       ii. Use _schemaService.GetLookupTableIdAsync to get the ID.
        //       iii. Replace the placeholder value in the data with the retrieved ID.
        //    b. Insert the prepared row into the root table using _dbRepository.
        //    c. Get the primary key of the newly inserted root row.
        // 4. Move to the child tables.
        // 5. For each row in a child table:
        //    a. Add the parent's primary key to the child row's foreign key column.
        //    b. Perform any other necessary lookups for the child row.
        //    c. Insert the child row.
        // 6. Repeat for all levels of the hierarchy.
        await Task.CompletedTask;
    }
}

