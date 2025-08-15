using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class MasterTestDefinition
{
    // Properties that must exist in the JSON
    public required string TestCaseName { get; set; }
    public required List<StepInstruction> Arrange { get; init; }
    public required List<StepInstruction> Actions { get; init; }
    public required List<ExpectedStateAssertion> Assert { get; init; }
    public required List<StepInstruction> Teardown { get; init; } = [];

    // Optional property in the JSON (can be null)
    public string? DataMapFile { get; init; }

    // A property NOT from the JSON that MUST be set manually after deserialization
    [JsonIgnore]
    public required string DefinitionFilePath { get; set; }
}