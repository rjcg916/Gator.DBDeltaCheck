using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
/// Defines the setup and "before" state of the system for a test.
/// Supports a list of generic setup actions.
/// </summary>
public class ArrangeDefinition
{
    [JsonProperty("actions")]
    public List<SetupActionInstruction> Actions { get; set; } = new List<SetupActionInstruction>();
}
