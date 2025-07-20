using DBDeltaCheck.Core.Abstractions;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions;
public class HttpDurableFunctionClient : IDurableFunctionClient
{
    Task<string> IDurableFunctionClient.StartOrchestrationAsync(string functionName, object payload)
    {
        throw new NotImplementedException();
    }

    Task<DurableFunctionStatus> IDurableFunctionClient.WaitForOrchestrationCompletionAsync(string instanceId, TimeSpan timeout)
    {
        throw new NotImplementedException();
    }
}
