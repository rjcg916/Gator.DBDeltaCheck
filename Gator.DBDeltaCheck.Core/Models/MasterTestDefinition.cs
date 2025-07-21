using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
/// Represents the root of a single, complete test case definition.
/// This class is the C# representation of the master JSON test file.
/// </summary>
public class MasterTestDefinition
{
    /// <summary>
    /// A descriptive name for the test case, used for logging and test runner output.
    /// </summary>
    [JsonProperty("testName")]
    public string TestName { get; set; }

    /// <summary>
    /// Defines all the setup and "before" state for the system under test.
    /// </summary>
    [JsonProperty("arrange")]
    public ArrangeDefinition Arrange { get; set; }

    /// <summary>
    /// Defines the primary action to be performed against the system.
    /// </summary>
    [JsonProperty("act")]
    public ActDefinition Act { get; set; }

    /// <summary>
    /// Defines all the validation steps to be performed after the action.
    /// </summary>
    [JsonProperty("assert")]
    public AssertDefinition Assert { get; set; }

    [JsonProperty("teardown")]
    public TeardownDefinition Teardown { get; set; } 
}
