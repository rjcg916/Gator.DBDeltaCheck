using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// Represents a single assertion to be made against the state of a database table.
/// </summary>
public class ExpectedStateAssertion
{
    /// <summary>
    /// The name of the database table to validate.
    /// </summary>

    public string Table { get; set; }

    /// <summary>
    /// The relative path to a JSON file containing the expected final state of the table.
    /// </summary>

    public string ExpectedDataFilePath { get; set; }

    /// <summary>
    /// Defines the comparison algorithm and options to use when comparing the actual and expected states.
    /// </summary>

    public ComparisonStrategyDefinition ComparisonStrategy { get; set; }
}