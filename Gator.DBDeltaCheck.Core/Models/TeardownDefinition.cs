using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// Defines the cleanup actions to be performed after a test run.
/// </summary>
public class TeardownDefinition
{
    public List<TeardownInstruction> Actions { get; set; } = new List<TeardownInstruction>();
}