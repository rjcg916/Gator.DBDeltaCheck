using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class OutputInstruction
{
    [JsonProperty("VariableName")] public required string VariableName { get; set; }

    [JsonProperty("Source")] public required OutputSource Source { get; set; }
}