using System.Threading.Tasks;

namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IDurableFunctionClient
{
    Task<OrchestrationStartResponse> StartDurableFunctionAsync(string orchestratorName, object payload);
    Task<DurableFunctionStatus> MonitorDurableFunctionStatusAsync(string statusQueryGetUri, int timeoutSeconds, string expectedStatus);
}


public record OrchestrationStartResponse(string id, string statusQueryGetUri, string sendEventPostUri, string terminatePostUri, string purgeHistoryDeleteUri);
public record DurableFunctionStatus(string name, string instanceId, string runtimeStatus, object input, object output, string createdTime, string lastUpdatedTime);
