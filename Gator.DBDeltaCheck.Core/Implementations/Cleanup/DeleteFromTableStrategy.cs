using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Cleanup;

public class DeleteFromTableStrategy : ICleanupStrategy
{
    public string StrategyName => throw new NotImplementedException();

    public async Task ExecuteAsync(IDatabaseRepository repository, JObject config)
    {
        var table = config["table"].Value<string>();
        var whereClause = config["whereClause"]?.Value<string>(); // Optional where clause

                
    //    await repository.DeleteFromTableAsync(table, whereClause);
    }

    public Task ExecuteAsync(object parameters)
    {
        throw new NotImplementedException();
    }
}