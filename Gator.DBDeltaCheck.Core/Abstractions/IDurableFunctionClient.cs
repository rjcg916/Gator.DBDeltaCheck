namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IDurableFunctionClient
{
    Task<OrchestrationStartResponse?> StartDurableFunctionAsync(string orchestratorName, string payloadJson);

    Task<DurableFunctionStatus> MonitorDurableFunctionStatusAsync(string statusQueryGetUri, int timeoutSeconds,
        string expectedStatus);
}

public record OrchestrationStartResponse(
    string Id,
    string StatusQueryGetUri,
    string SendEventPostUri,
    string TerminatePostUri,
    string PurgeHistoryDeleteUri);

public record DurableFunctionStatus(
    string Name,
    string InstanceId,
    string RuntimeStatus,
    object Input,
    object Output,
    string CreatedTime,
    string LastUpdatedTime);