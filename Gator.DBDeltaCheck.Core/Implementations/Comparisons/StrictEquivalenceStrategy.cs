using DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Comparisons;
public class StrictEquivalenceStrategy : IComparisonStrategy
{
    void IComparisonStrategy.AssertState(object actualState, object expectedState, JObject options)
    {
        throw new NotImplementedException();
    }
}
