using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class MasterTestDefinition
{
    /// <summary>
    ///     A descriptive name for the test case, used for logging and test runner output.
    /// </summary>
    public required string TestCaseName { get; set; }

    [JsonIgnore] public required string DefinitionFilePath { get; set; }

    public required List<StepInstruction> Arrangements { get; set; }

    public required List<StepInstruction> Actions { get; set; }

    public required List<ExpectedStateAssertion> Assertions { get; set; }

    public required List<StepInstruction> Teardowns { get; set; }
}