using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Seeding;

/// <summary>
/// An abstract base class for setup strategies that provides common functionality,
/// such as processing output variables.
/// </summary>
public abstract class BaseSetupStrategy : ISetupStrategy
{
    protected readonly IDatabaseRepository Repository;

    protected BaseSetupStrategy(IDatabaseRepository repository)
    {
        Repository = repository;
    }

    // Derived classes must provide their unique name.
    public abstract string StrategyName { get; }

    // Derived classes must implement the core execution logic.
    public abstract Task ExecuteAsync(JObject parameters, Dictionary<string, object> testContext, DataMap? dataMap);

    /// <summary>
    /// The shared logic for processing the "Outputs" section of the parameters
    /// to capture seeded data into the test context.
    /// </summary>
    protected async Task ProcessOutputs(JArray outputsArray, Dictionary<string, object> testContext)
    {
        var outputInstructions = outputsArray.ToObject<List<OutputInstruction>>();
        if (outputInstructions == null) return;

        foreach (var instruction in outputInstructions)
        {
            var source = instruction.Source;
            var sql = $"SELECT TOP 1 {source.SelectColumn} FROM {source.FromTable} ORDER BY {source.OrderByColumn} {source.OrderDirection ?? "DESC"}";
            var outputValue = await Repository.ExecuteScalarAsync<object>(sql);
            if (outputValue != null)
            {
                testContext[instruction.VariableName] = outputValue;
            }
        }
    }
}
