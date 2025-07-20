using DB.IntegrationTests.Tests;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

// Represents a setup action (e.g., seeding data)
public interface ISetupStrategy
{
    Task ExecuteAsync(IDatabaseRepository repository, JObject config);
}
