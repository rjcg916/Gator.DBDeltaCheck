using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class MasterTestDefinition
{
    public required string TestCaseName { get; set; }

    [JsonProperty("DataMapFile")] public string? DataMapFile { get; set; }

    [JsonIgnore] public required string DefinitionFilePath { get; set; }

    public required List<StepInstruction> Arrange { get; set; }

    public required List<StepInstruction> Actions { get; set; }

    public required List<ExpectedStateAssertion> Assert { get; set; }

    public required List<StepInstruction> Teardown { get; set; }
}