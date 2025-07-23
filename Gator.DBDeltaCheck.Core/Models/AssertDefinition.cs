using Newtonsoft.Json;

namespace Gator.DBDeltaCheck.Core.Models;

public class AssertDefinition
{
    public List<ExpectedStateAssertion> ExpectedState { get; set; } = new List<ExpectedStateAssertion>();
}
