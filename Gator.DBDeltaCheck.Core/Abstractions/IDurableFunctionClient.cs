namespace DBDeltaCheck.Core.Abstractions;

public enum DurableFunctionStatus { Completed, Failed, Terminated, Running, Pending }

public interface IDurableFunctionClient
{
    Task<string> StartOrchestrationAsync(string functionName, object payload);
    Task<DurableFunctionStatus> WaitForOrchestrationCompletionAsync(string instanceId, TimeSpan timeout);
}