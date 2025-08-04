using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class MasterTestDefinition
{
    /// <summary>
    ///     A descriptive name for the test case, used for logging and test runner output.
    /// </summary>
    public string TestCaseName { get; set; }

    [JsonIgnore] public string DefinitionFilePath { get; set; }

    public List<StepInstruction> Arrangements { get; set; }

    public List<StepInstruction> Actions { get; set; }

    public List<ExpectedStateAssertion> Assertions { get; set; }

    public List<StepInstruction> Teardowns { get; set; }
}