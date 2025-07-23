using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class DurableFunctionAction
{

    [JsonProperty("functionName")]
    public string FunctionName { get; set; }

    [JsonProperty("payload")]
    public object Payload { get; set; }
}