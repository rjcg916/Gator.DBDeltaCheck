using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions;

public class DurableFunctionActionStrategy : IActionStrategy
{
    private readonly IDurableFunctionClient _durableFunctionClient;

    public DurableFunctionActionStrategy(IDurableFunctionClient durableFunctionClient)
    {
        _durableFunctionClient = durableFunctionClient;
    }

    public string StrategyName => "DurableFunction";

    public async Task<bool> ExecuteAsync(JObject parameters)
    {
        // 1. Get configuration from the JSON parameters.
        var orchestratorName = parameters["OrchestratorName"]?.Value<string>()
                               ?? throw new ArgumentException(
                                   "'OrchestratorName' is missing from DurableFunction parameters.");

        var payloadToken = parameters["Payload"]
                           ?? throw new ArgumentException("'Payload' is missing from DurableFunction parameters.");
        var payload = payloadToken.ToString();

        var timeout = parameters["PollingTimeoutSeconds"]?.Value<int>() ?? 300; // Default to 5 minutes
        var expectedStatus = parameters["ExpectedStatus"]?.Value<string>() ?? "Completed";

        // 2. Start the durable function.
        var startResponse = await _durableFunctionClient.StartDurableFunctionAsync(orchestratorName, payload);

        // 3. Monitor the function until it completes.
        // This will throw an exception on failure or timeout, which correctly fails the test.
        await _durableFunctionClient.MonitorDurableFunctionStatusAsync(
            startResponse.StatusQueryGetUri,
            timeout,
            expectedStatus);

        return true; // If we get here, the test step was successful.
    }
}