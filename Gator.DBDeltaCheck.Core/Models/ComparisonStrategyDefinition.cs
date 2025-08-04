using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

public class ComparisonStrategyDefinition
{
    public string Strategy { get; set; }

    public JObject Parameters { get; set; }
}