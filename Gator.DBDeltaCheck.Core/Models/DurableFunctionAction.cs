using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class DurableFunctionAction
{

    public string FunctionName { get; set; }

    public object Payload { get; set; }
}