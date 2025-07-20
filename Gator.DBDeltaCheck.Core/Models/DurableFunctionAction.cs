using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// Contains the details required to trigger an Azure Durable Function.
/// </summary>
public class DurableFunctionAction
{
    /// <summary>
    /// The name of the orchestrator function to start.
    /// </summary>
    [JsonProperty("functionName")]
    public string FunctionName { get; set; }

    /// <summary>
    /// The JSON payload to pass as input to the orchestrator function. Can be any valid JSON object.
    /// </summary>
    [JsonProperty("payload")]
    public object Payload { get; set; }
}