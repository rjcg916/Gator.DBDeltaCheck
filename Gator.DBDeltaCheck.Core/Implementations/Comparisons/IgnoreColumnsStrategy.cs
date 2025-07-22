using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Comparisons;
public class IgnoreColumnsStrategy : IComparisonStrategy
{
    void IComparisonStrategy.AssertState(object actualState, object expectedState, JObject options)
    {
        throw new NotImplementedException();
    }
}
