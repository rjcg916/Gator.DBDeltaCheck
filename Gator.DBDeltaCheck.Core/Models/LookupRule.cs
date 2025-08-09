using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class LookupRule
{
    [JsonProperty("dataProperty")]
    public required string DataProperty { get; set; }

    [JsonProperty("lookupTable")]
    public required string LookupTable { get; set; }

    [JsonProperty("lookupValueColumn")]
    public required string LookupValueColumn { get; set; }
}