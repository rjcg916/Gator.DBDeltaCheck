using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;
/// <summary>
/// Defines the contract for a specific mapping operation.
/// </summary>
public interface IMappingStrategy
{
    Task Apply(JObject record, string tableName, TableMap tableMap);
}
