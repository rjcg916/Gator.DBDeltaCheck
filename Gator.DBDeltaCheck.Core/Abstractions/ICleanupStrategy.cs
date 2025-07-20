using DB.IntegrationTests.Tests;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

// Represents a cleanup action (e.g., deleting data)
public interface ICleanupStrategy
{
    Task ExecuteAsync(IDatabaseRepository repository, JObject config);
}