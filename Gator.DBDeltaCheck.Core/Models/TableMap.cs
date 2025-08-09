using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;
public class TableMap
{
    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("lookups")]
    public required List<LookupRule> Lookups { get; set; }
}

