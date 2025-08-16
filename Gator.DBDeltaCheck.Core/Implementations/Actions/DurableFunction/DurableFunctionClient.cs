using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gator.DBDeltaCheck.Core.Abstractions;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions.DurableFunction;

public class DurableFunctionClient(HttpClient httpClient) : IDurableFunctionClient
{
    public async Task<OrchestrationStartResponse?> StartDurableFunctionAsync(string orchestratorName, string payloadJson)
    {
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"orchestrators/{orchestratorName}", content);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrchestrationStartResponse>();
    }

    public async Task<DurableFunctionStatus> MonitorDurableFunctionStatusAsync(string statusQueryGetUri,
        int timeoutSeconds, string expectedStatus)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var terminalStates = new HashSet<string> { "Completed", "Failed", "Terminated", "Canceled" };

        while (!cts.IsCancellationRequested)
        {
            var response = await httpClient.GetAsync(statusQueryGetUri, cts.Token);
            response.EnsureSuccessStatusCode();
            var status = await response.Content.ReadFromJsonAsync<DurableFunctionStatus>(cts.Token);

            if (status != null && terminalStates.Contains(status.RuntimeStatus))
            {
                if (status.RuntimeStatus == expectedStatus) return status; // Success!
                var outputJson = JsonSerializer.Serialize(status.Output);
                throw new InvalidOperationException(
                    $"Orchestration reached terminal state '{status.RuntimeStatus}', but expected '{expectedStatus}'. Output: {outputJson}");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cts.Token); // Poll every 5 seconds
        }

        throw new TimeoutException($"Orchestration did not complete within the {timeoutSeconds}s timeout.");
    }
}