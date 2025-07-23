using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;


public class JsonFileSeedingStrategy : ISetupStrategy
{
    /// <summary>
    /// The name used in the test definition JSON to select this strategy.
    /// </summary>
    public string StrategyName => "JsonFileSeed";

    /// <summary>
    /// Executes a simple data seed by inserting all records from a single JSON file into a single table.
    /// </summary>
    public async Task ExecuteAsync(IDatabaseRepository repository, JObject config)
    {
        // 1. Validate configuration and throw informative errors
        var tableName = config["table"]?.Value<string>()
            ?? throw new ArgumentException("The 'table' property is missing in the JsonFileSeed config.");

        var dataFilePath = config["dataFile"]?.Value<string>()
            ?? throw new ArgumentException("The 'dataFile' property is missing in the JsonFileSeed config.");

        // 2. Resolve the data file's path relative to the main test definition file.
        // The test runner is responsible for adding this "_basePath" to the config.
        var basePath = config["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();
        var absoluteDataFilePath = Path.Combine(basePath, dataFilePath);

        if (!File.Exists(absoluteDataFilePath))
        {
            throw new FileNotFoundException($"The specified data file was not found: {absoluteDataFilePath}");
        }

        var seedContent = await File.ReadAllTextAsync(absoluteDataFilePath);

        // 3. Keep logic within the strategy; repository stays generic.
        var records = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(seedContent);
        if (records == null) return;

        foreach (var record in records)
        {
            // Use a generic insert method. We don't need the ID back for this simple strategy.
            await repository.InsertRecordAsync(tableName, record);
        }
    }
}