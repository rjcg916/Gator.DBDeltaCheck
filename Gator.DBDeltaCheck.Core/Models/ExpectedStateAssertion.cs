using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
///     Represents a single assertion to be made against the state of a database table.
/// </summary>
public class ExpectedStateAssertion
{
    [JsonProperty("TableName")] 
    public required string TableName { get; set; }
    [JsonProperty("ExpectedDataFile")] 
    public required string ExpectedDataFile { get; set; }
    [JsonProperty("DataMapFile")]
    public string? DataMapFile { get; set; }
    [JsonProperty("AssertionStrategy")] 
    public string Strategy { get; set; } = "Flat";
    [JsonProperty("AssertionParameters")] 
    public JObject? Parameters { get; set; }
}