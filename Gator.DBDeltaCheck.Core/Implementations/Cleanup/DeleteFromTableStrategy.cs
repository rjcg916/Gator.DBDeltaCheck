using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Gator.DBDeltaCheck.Core.Implementations.Cleanup
{
    public class DeleteFromTableStrategy : ICleanupStrategy
    {
        public string StrategyName => "DeleteFromTable";

        // The repository is now a private field, provided by the constructor.
        private readonly IDatabaseRepository _repository;

        /// <summary>
        /// The strategy's dependencies are injected via the constructor by the DI container.
        /// </summary>
        public DeleteFromTableStrategy(IDatabaseRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Executes a DELETE statement against a specified table.
        /// </summary>
        // The method signature is now clean and only accepts the parameters.
        public async Task ExecuteAsync(JObject parameters)
        {
            var table = parameters["table"]?.Value<string>()
                ?? throw new System.ArgumentException("The 'table' property is missing in the DeleteFromTable config.");

            var whereClause = parameters["whereClause"]?.Value<string>();

            var sql = $"DELETE FROM {table}" +
                      (whereClause != null ? $" WHERE {whereClause}" : string.Empty);

            // Use the injected repository instance.
            await _repository.ExecuteAsync(sql);
        }
    }
}
