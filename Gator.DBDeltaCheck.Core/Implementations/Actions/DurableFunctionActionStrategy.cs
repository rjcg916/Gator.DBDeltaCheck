using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions;
public class DurableFunctionActionStrategy : IActionStrategy
{
    public string StrategyName => "DurableFunction";

    private readonly IDurableFunctionClient _durableFunctionClient;

    public DurableFunctionActionStrategy(IDurableFunctionClient durableFunctionClient)
    {
        _durableFunctionClient = durableFunctionClient;
    }

    public async Task<bool> ExecuteAsync(JObject config)
    {
        var functionName = config["functionName"].Value<string>();
        var payload = config["payload"] as JObject;
        var timeout = config["timeoutMinutes"]?.Value<int>() ?? 5;

        var instanceId = await _durableFunctionClient.StartOrchestrationAsync(functionName, payload);

        var finalStatus = await _durableFunctionClient.WaitForOrchestrationCompletionAsync(
            instanceId,
            TimeSpan.FromMinutes(timeout));

        // Return true if the function completed successfully
        return finalStatus == DurableFunctionStatus.Completed;
    }

}