using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class OutputSource
{
    [JsonProperty("FromTable")] public required string FromTable { get; set; }

    [JsonProperty("SelectColumn")] public required string SelectColumn { get; set; }

    [JsonProperty("OrderByColumn")] public required string OrderByColumn { get; set; }

    [JsonProperty("OrderDirection")] public string? OrderDirection { get; set; }
}