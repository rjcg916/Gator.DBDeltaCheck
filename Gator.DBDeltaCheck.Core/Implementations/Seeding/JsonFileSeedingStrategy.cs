using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;

public class JsonFileSeedingStrategy(IDatabaseRepository repository) : ISetupStrategy
{
    public string StrategyName => "JsonFileSeed";

    /// <summary>
    ///     Executes a simple data seed by inserting all records from a single JSON file into a single table.
    /// </summary>
    public async Task Setup(JObject parameters, Dictionary<string, object> testContext, DataMap? dataMap = null)
    {

        // 1. Get all parameters from the JSON.
        var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();

        var dataFilePath = parameters["dataFile"]?.Value<string>()
                           ?? throw new ArgumentException(
                               "The 'dataFile' property is missing in the JsonFileSeed config.");
        var absoluteDataFilePath = Path.Combine(basePath, dataFilePath);

        var tableName = parameters["table"]?.Value<string>()
                        ?? throw new ArgumentException("The 'table' property is missing in the JsonFileSeed config.");

        var allowIdentityInsert = parameters["allowIdentityInsert"]?.Value<bool>() ?? false;


        // 2. Resolve the absolute path and check if the file exists.
        if (!File.Exists(absoluteDataFilePath))
            throw new FileNotFoundException($"The specified data file was not found: {absoluteDataFilePath}");

        // 3. Read the JSON file and deserialize it into a list of records.
        var seedContent = await File.ReadAllTextAsync(absoluteDataFilePath);
        var records = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(seedContent);
        if (records == null) return;

        // 4. Seeding the data into the specified table.
        foreach (var record in records)
            await repository.InsertRecordAsync(tableName, record, allowIdentityInsert);

        // 5. If there are any output instructions, process them.
        if (parameters.TryGetValue("Outputs", out var outputsToken) && outputsToken is JArray outputsArray)
        {
            await ProcessOutputs(outputsArray, testContext);
        }
    }

    private async Task ProcessOutputs(JArray outputsArray, Dictionary<string, object> testContext)
    {
        var outputInstructions = outputsArray.ToObject<List<OutputInstruction>>();
        if (outputInstructions == null) return;

        foreach (var instruction in outputInstructions)
        {
            var source = instruction.Source;
            var sql = $"SELECT TOP 1 {source.SelectColumn} FROM {source.FromTable} ORDER BY {source.OrderByColumn} {source.OrderDirection ?? "DESC"}";
            var outputValue = await repository.ExecuteScalarAsync<object>(sql);
            if (outputValue != null)
            {
                testContext[instruction.VariableName] = outputValue;
            }
        }
    }
}