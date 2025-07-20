using Newtonsoft.Json.Linq;

namespace DBDeltaCheck.Core.Abstractions;

public interface IComparisonStrategy
{
    void AssertState(object actualState, object expectedState, JObject options);
}

