using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;
/// <summary>
/// Defines a service that resolves placeholder values in an expected state JSON file
/// using a data map before assertion.
/// </summary>
public interface IExpectedStateResolver
{
    /// <summary>
    /// Takes a raw JSON token and a data map, and resolves any lookups.
    /// </summary>
    /// <param name="expectedStateToken">The JToken representing the expected state.</param>
    /// <param name="dataMap">The loaded data map containing lookup rules.</param>
    /// <returns>A new JToken with all lookups resolved to actual database values.</returns>
    Task<JToken> Resolve(JToken rawExpectedStateJson, DataMap dataMap, string tableName);

}