using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class ActDefinition
{
    // The 'durableFunction' property is replaced by a list of generic 'actions'.
    [JsonProperty("actions")]
    public List<ActionInstruction> Actions { get; set; } = new List<ActionInstruction>();
}