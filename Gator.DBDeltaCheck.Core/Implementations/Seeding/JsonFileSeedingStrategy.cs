using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;
public class JsonFileSeedingStrategy : ISetupStrategy
{
    public async Task ExecuteAsync(IDatabaseOperations repository, JObject config)
    {
        var table = config["table"].Value<string>();
        var dataFile = config["dataFile"].Value<string>();

        // Assumes data files are in a known location, e.g., "TestData"
        var seedContent = File.ReadAllText(Path.Combine("TestData", dataFile));

        // The repository handles the generic logic of deserializing and inserting
        await repository.SeedTableAsync(table, seedContent);

    }

}