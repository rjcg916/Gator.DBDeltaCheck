using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class ArrangeDefinition
{
    [JsonProperty("actions")]
    public List<ArrangeInstruction> Actions { get; set; } = new List<ArrangeInstruction>();
}