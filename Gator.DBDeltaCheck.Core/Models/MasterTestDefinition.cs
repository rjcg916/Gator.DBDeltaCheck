using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;


public class MasterTestDefinition
{
    /// <summary>
    /// A descriptive name for the test case, used for logging and test runner output.
    /// </summary>
    [JsonProperty("testName")]
    public string TestName { get; set; }

    [JsonProperty("arrange")]
    public ArrangeDefinition Arrange { get; set; }

    [JsonProperty("act")]
    public ActionDefinition Action { get; set; }

    [JsonProperty("assert")]
    public AssertDefinition Assert { get; set; }

    [JsonProperty("teardown")]
    public TeardownDefinition Teardown { get; set; } 
}
