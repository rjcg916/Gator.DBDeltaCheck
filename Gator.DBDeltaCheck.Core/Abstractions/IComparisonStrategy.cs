using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IComparisonStrategy
{
    void AssertState(object actualState, object expectedState, JObject options);
}

