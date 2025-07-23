using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class ActionDefinition
{
    [JsonProperty("actions")]
    public List<ActionInstruction> Actions { get; set; } = new List<ActionInstruction>();
}