namespace Gator.DBDeltaCheck.Core.Models;

public class DurableFunctionAction
{
    public required string FunctionName { get; set; }
    public required object Payload { get; set; }
}