using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// A generic instruction for a single setup action.
/// </summary>
public class ArrangeInstruction
{
    public string Strategy { get; set; }

    public JObject Parameters { get; set; }
}