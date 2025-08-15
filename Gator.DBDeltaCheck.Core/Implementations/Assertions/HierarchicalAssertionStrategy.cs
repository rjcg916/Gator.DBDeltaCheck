using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Abstractions.Factories;
using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Assertions;

public class HierarchicalAssertionStrategy(
    DbContext dbContext,
    IDataMapper dataMapper,
    IDataComparisonRuleFactory ruleFactory)
    : IAssertionStrategy
{
    public string StrategyName => "HierarchicalAssert";

    // Depends on the rule factory

    public async Task ExecuteAsync(JObject parameters, Dictionary<string, object> context, DataMap? dataMap)
    {

        // 1. Get all parameters from the JSON.

        var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();
        var expectedDataFile = parameters["ExpectedDataFile"]?.Value<string>() ?? throw new ArgumentException("'ExpectedDataFile' is missing.");


        var rootEntityName = parameters["RootEntity"]?.Value<string>() ?? throw new ArgumentException("'RootEntity' is missing.");
        var findByIdToken = parameters["FindById"]?.Value<string>() ?? throw new ArgumentException("'FindById' is missing.");
        var includePaths = parameters["IncludePaths"]?.ToObject<List<string>>() ?? [];

        var comparisonRuleInfo = parameters["ComparisonRule"]?.ToObject<ComparisonRuleInfo>() ?? new ComparisonRuleInfo();

        // 2. Perform the "deep query" to get the raw actual object.
        var idToFind = context[findByIdToken.Trim('{', '}')];
        var rawActualObject = await QueryHierarchy(rootEntityName, idToFind, includePaths);
        var rawActualJson = JsonConvert.SerializeObject(rawActualObject);

        // 3. Load files and map the actual state to a "friendly" format.
        var expectedDataPath = Path.Combine(basePath, expectedDataFile);
        var expectedStateJson = await File.ReadAllTextAsync(expectedDataPath);
        var mappedActualJson = await dataMapper.MapToFriendlyState(rawActualJson, dataMap ?? new DataMap() { Tables = []
        }, rootEntityName);

        // 4. Get the correct comparison rule and execute it.
        var comparisonRule = ruleFactory.GetStrategy(comparisonRuleInfo.Name);
        var areEqual = comparisonRule.Compare(mappedActualJson, expectedStateJson, comparisonRuleInfo.Parameters);

        areEqual.Should().BeTrue($"Hierarchical comparison failed for {rootEntityName} with ID {idToFind}.");
    }

    private async Task<object?> QueryHierarchy(string rootEntityName, object id, List<string> includePaths)
    {
        var entityType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.ClrType.Name.Equals(rootEntityName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Entity '{rootEntityName}' not found in the DbContext.");

        // 1. Get the generic Set<T>() method.
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)
                                         ?.MakeGenericMethod(entityType.ClrType)
            ?? throw new InvalidOperationException("Could not find the 'Set' method on DbContext.");

        var dbSet = setMethod.Invoke(dbContext, null)
            ?? throw new InvalidOperationException($"DbContext.Set<{entityType.ClrType.Name}>() returned null.");

        var query = (IQueryable)dbSet;

        // 2. Find and apply the generic Include method.
        var includeMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType == typeof(string))
            ?.MakeGenericMethod(entityType.ClrType)
            ?? throw new InvalidOperationException("Could not find the 'Include' extension method.");

        foreach (var path in includePaths)
        {
            query = (IQueryable)(includeMethod.Invoke(null, [query, path])
                ?? throw new InvalidOperationException("The .Include() method returned null."));
        }

        // 3. Find and apply the generic AsNoTracking method.
        var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethod(nameof(EntityFrameworkQueryableExtensions.AsNoTracking), BindingFlags.Public | BindingFlags.Static)
            ?.MakeGenericMethod(entityType.ClrType)
            ?? throw new InvalidOperationException("Could not find the 'AsNoTracking' extension method.");

        query = (IQueryable)(asNoTrackingMethod.Invoke(null, [query])
            ?? throw new InvalidOperationException("The .AsNoTracking() method returned null."));

        // 4. Build the dynamic WHERE clause.
        var pkProperty = entityType.FindPrimaryKey()?.Properties.First()
            ?? throw new InvalidOperationException($"Entity '{entityType.ClrType.Name}' does not have a primary key defined.");

        var parameter = Expression.Parameter(entityType.ClrType, "p");
        var pkPropertyExpression = Expression.Property(parameter, pkProperty.Name);
        var convertedId = Convert.ChangeType(id, pkProperty.ClrType);
        var idValueExpression = Expression.Constant(convertedId);
        var equalsExpression = Expression.Equal(pkPropertyExpression, idValueExpression);
        var lambda = Expression.Lambda(equalsExpression, parameter);

        // 5. Find and invoke the generic FirstOrDefaultAsync method.
        var firstOrDefaultMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == nameof(EntityFrameworkQueryableExtensions.FirstOrDefaultAsync) && m.GetParameters().Length == 3)
            ?.MakeGenericMethod(entityType.ClrType)
            ?? throw new InvalidOperationException("Could not find the 'FirstOrDefaultAsync' extension method.");

        var task = (Task)(firstOrDefaultMethod.Invoke(null, [query, lambda, CancellationToken.None])
            ?? throw new InvalidOperationException("Invoking FirstOrDefaultAsync returned null."));

        await task;

        // 6. Get the result from the completed task.
        var result = ((dynamic)task).Result;
        return result;
    }
    internal class ComparisonRuleInfo
    {
        public string Name { get; set; } = "IgnoreOrder";
        public JToken? Parameters { get; set; }
    }
}