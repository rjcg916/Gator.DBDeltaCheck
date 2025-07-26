using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class MasterTestDefinition
{
    /// <summary>
    /// A descriptive name for the test case, used for logging and test runner output.
    /// </summary>
    public string TestCaseName { get; set; }

    [JsonIgnore]
    public string DefinitionFilePath { get; set; }

    public ArrangeDefinition Arrange { get; set; }

    public ActionDefinition Action { get; set; }

    public AssertDefinition Assert { get; set; }

    public TeardownDefinition Teardown { get; set; } 
}
