using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// A generic instruction for a single cleanup action.
/// </summary>
public class CleanupActionInstruction
{
    /// <summary>
    /// The type of cleanup action. E.g., "DeleteFromTable", "ExecuteSqlScript".
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; }

    /// <summary>
    /// Configuration specific to this cleanup action type.
    /// </summary>
    [JsonProperty("config")]
    public JObject Config { get; set; }
}