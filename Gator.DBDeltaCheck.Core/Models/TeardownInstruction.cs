using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

public class TeardownInstruction
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("config")]
    public JObject Config { get; set; }
}