using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
/// Defines the action(s) to be performed against the system under test.
/// Supports a list of actions to be executed sequentially.
/// </summary>
public class ActDefinition
{
    // The 'durableFunction' property is replaced by a list of generic 'actions'.
    [JsonProperty("actions")]
    public List<ActionInstruction> Actions { get; set; } = new List<ActionInstruction>();
}