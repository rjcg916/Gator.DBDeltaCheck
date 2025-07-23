using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class ActionDefinition
{
    public List<ActionInstruction> Actions { get; set; } = new List<ActionInstruction>();
}