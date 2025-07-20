using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// A generic instruction for a single setup action.
/// </summary>
public class SetupActionInstruction
{
    /// <summary>
    /// The type of setup action. E.g., "SeedFromJson", "ExecuteSqlScript".
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; }

    /// <summary>
    /// Configuration specific to this setup action type.
    /// </summary>
    [JsonProperty("config")]
    public JObject Config { get; set; }
}