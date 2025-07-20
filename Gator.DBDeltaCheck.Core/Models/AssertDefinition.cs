using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
/// Defines the "Assert" phase of the test, containing all post-test validations.
/// </summary>
public class AssertDefinition
{
    /// <summary>
    /// A list of assertions to make against the final state of the database.
    /// </summary>
    [JsonProperty("expectedState")]
    public List<ExpectedStateAssertion> ExpectedState { get; set; } = new List<ExpectedStateAssertion>();
}
