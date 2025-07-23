using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;

public class TeardownInstruction
{
    public string Strategy { get; set; }

    public JObject Parameters { get; set; }
}