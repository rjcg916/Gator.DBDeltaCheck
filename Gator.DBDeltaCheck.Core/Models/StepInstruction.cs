using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
///     A generic instruction for a single setup action.
/// </summary>
public class StepInstruction
{
    public string Strategy { get; set; }

    public JObject Parameters { get; set; }
}