namespace Gator.DBDeltaCheck.Core.Abstractions.Factories;

public interface IDataComparisonRuleFactory
{
    /// <summary>
    /// Creates a data comparison rule based on the provided rule name.
    /// </summary>
    /// <param name="ruleName">The name of the rule to create.</param>
    /// <returns>An instance of IDataComparisonRule.</returns>
    IDataComparisonRule GetStrategy(string ruleName);
}