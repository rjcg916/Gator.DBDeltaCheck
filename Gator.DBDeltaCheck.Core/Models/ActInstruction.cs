using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// A generic instruction for a single action to be performed.
/// </summary>
public class ActionInstruction
{
    /// <summary>
    /// The type of action to perform. This will be used by a factory
    /// to create the correct action strategy.
    /// E.g., "DurableFunction", "ApiCall", "QueueMessage"
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; }

    /// <summary>
    /// A flexible JSON object containing the specific configuration
    /// needed for this action type.
    /// </summary>
    [JsonProperty("config")]
    public JObject Config { get; set; }
}