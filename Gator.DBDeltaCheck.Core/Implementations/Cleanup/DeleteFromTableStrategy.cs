using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Cleanup;

public class DeleteFromTableStrategy : ICleanupStrategy
{
    public string StrategyName => "DeleteFromTable";

    public async Task ExecuteAsync(IDatabaseRepository repository, JObject config)
    {
        var table = config["table"].Value<string>();
        var whereClause = config["whereClause"]?.Value<string>(); 
        var sql = $"DELETE FROM {table}" + 
                    (whereClause != null ? $" WHERE {whereClause}" : string.Empty);  
        await repository.ExecuteAsync(sql);                

    }

}