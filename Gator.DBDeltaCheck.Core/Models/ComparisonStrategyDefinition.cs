using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

public class ComparisonStrategyDefinition
{
    public required string Strategy { get; set; }
    public required JObject Parameters { get; set; }
}