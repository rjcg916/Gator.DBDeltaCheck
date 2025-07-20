using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.Data.SqlClient;
using Respawn;

namespace Gator.DBDeltaCheck.Core.Implementations.Cleanup;
public class RespawnCleanupStrategy : ICleanupStrategy
{
    private readonly Respawner _respawner;
    public string StrategyName => "Respawn";

    public RespawnCleanupStrategy(Respawner respawner)
    {
        _respawner = respawner;
    }

    public async Task ExecuteAsync(object parameters)
    {
        // Parameters could include the connection string if not already configured
        // but here we assume it's injected via DI.
        var connectionString = parameters.ToString();
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }
}
