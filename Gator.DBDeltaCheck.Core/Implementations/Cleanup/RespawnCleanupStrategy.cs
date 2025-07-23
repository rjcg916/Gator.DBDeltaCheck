using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Respawn;
using System.Data.Common;

namespace Gator.DBDeltaCheck.Core.Implementations.Cleanup;
public class RespawnCleanupStrategy : ICleanupStrategy
{

    public string StrategyName => "Respawn";

    private readonly Respawner _respawner;

    public RespawnCleanupStrategy(Respawner respawner)
    {
        _respawner = respawner;
    }

    public async Task ExecuteAsync(IDatabaseRepository repository, JObject config)
    {
        var conn = repository.GetDbConnection();

        conn.Open();
        await _respawner.ResetAsync((DbConnection) conn);
      
    }
}
