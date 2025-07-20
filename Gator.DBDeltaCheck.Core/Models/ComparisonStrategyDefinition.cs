using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// Defines the comparison strategy to be used for an assertion, enabling flexible data comparison.
/// </summary>
public class ComparisonStrategyDefinition
{
    /// <summary>
    /// The name of the comparison strategy to use (e.g., "Strict", "IgnoreColumns", "IgnoreOrder").
    /// This name maps to a concrete implementation of the IComparisonStrategy interface.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// A flexible JSON object containing options specific to the chosen comparison strategy.
    /// For example, for "IgnoreColumns", this would contain an array of column names to exclude.
    /// </summary>
    [JsonProperty("options")]
    public JObject Options { get; set; }
}