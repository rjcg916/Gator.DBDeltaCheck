using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class DataMap
{
    [JsonProperty("tables")]
    public required List<TableMap> Tables { get; set; }
}
