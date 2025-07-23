using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

public class ComparisonStrategyDefinition
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("options")]
    public JObject Options { get; set; }
}