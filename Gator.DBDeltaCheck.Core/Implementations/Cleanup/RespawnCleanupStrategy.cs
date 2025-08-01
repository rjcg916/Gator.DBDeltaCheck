using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;
using Respawn;

namespace Gator.DBDeltaCheck.Core.Implementations.Cleanup
{
    public class RespawnCleanupStrategy : ICleanupStrategy
    {
        public string StrategyName => "Respawn";

        // The strategy now holds all the tools it needs.
        private readonly Task<Respawner> _respawnerTask;
        private readonly IDatabaseRepository _repository;

        /// <summary>
        /// The strategy's dependencies are injected via the constructor by the DI container.
        /// It takes a Task<Respawner> because Respawner is initialized asynchronously.
        /// </summary>
        public RespawnCleanupStrategy(Task<Respawner> respawnerTask, IDatabaseRepository repository)
        {
            _respawnerTask = respawnerTask;
            _repository = repository;
        }

        /// <summary>
        /// Resets the database to a clean state using the configured Respawner instance.
        /// </summary>
        // The method signature is now clean and only accepts the parameters (which are unused in this case).
        public async Task ExecuteAsync(JObject parameters)
        {
            // First, get the Respawner instance by awaiting the task.
            var respawner = await _respawnerTask;

            // Get a database connection from the repository.
            // The 'await using' statement ensures the connection is properly
            // opened, used, and then closed/disposed of, even if an error occurs.
            await using var connection = _repository.GetDbConnection();

            // Execute the reset operation.
            await respawner.ResetAsync(connection);
        }
    }
}
