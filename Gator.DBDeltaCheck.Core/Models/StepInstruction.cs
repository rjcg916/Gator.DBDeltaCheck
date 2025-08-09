using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
///     A generic instruction for a single setup action.
/// </summary>
public class StepInstruction
{
    public required string Strategy { get; set; }

    public required JObject Parameters { get; set; }
}