using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

public class ActionInstruction
{

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("config")]
    public JObject Config { get; set; }
}