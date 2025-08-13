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

public class HierarchicalAssertionStrategy : IAssertionStrategy
{
    public string StrategyName => "HierarchicalAssert";

    private readonly DbContext _dbContext;
    private readonly IDataMapper _dataMapper;
    private readonly IDataComparisonRuleFactory _ruleFactory; // Depends on the rule factory

    public HierarchicalAssertionStrategy(DbContext dbContext, IDataMapper dataMapper, IDataComparisonRuleFactory ruleFactory)
    {
        _dbContext = dbContext;
        _dataMapper = dataMapper;
        _ruleFactory = ruleFactory;
    }

    public async Task AssertState(JObject parameters, Dictionary<string, object> context, DataMap? dataMap)
    {

        // 1. Get all parameters from the JSON.

        var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();
        var expectedDataFile = parameters["ExpectedDataFile"]?.Value<string>() ?? throw new ArgumentException("'ExpectedDataFile' is missing.");


        var rootEntityName = parameters["RootEntity"]?.Value<string>() ?? throw new ArgumentException("'RootEntity' is missing.");
        var findByIdToken = parameters["FindById"]?.Value<string>() ?? throw new ArgumentException("'FindById' is missing.");
        var includePaths = parameters["IncludePaths"]?.ToObject<List<string>>() ?? new List<string>();

        var comparisonRuleInfo = parameters["ComparisonRule"]?.ToObject<ComparisonRuleInfo>() ?? new ComparisonRuleInfo();

        // 2. Perform the "deep query" to get the raw actual object.
        var idToFind = context[findByIdToken.Trim('{', '}')];
        var rawActualObject = await QueryHierarchy(rootEntityName, idToFind, includePaths);
        var rawActualJson = JsonConvert.SerializeObject(rawActualObject);

        // 3. Load files and map the actual state to a "friendly" format.
        var expectedDataPath = Path.Combine(basePath, expectedDataFile);
        var expectedStateJson = await File.ReadAllTextAsync(expectedDataPath);
        var mappedActualJson = await _dataMapper.MapToFriendlyState(rawActualJson, dataMap ?? new DataMap() { Tables = new List<TableMap>()}, rootEntityName);

        // 4. Get the correct comparison rule and execute it.
        var comparisonRule = _ruleFactory.GetStrategy(comparisonRuleInfo.Name);
        var areEqual = comparisonRule.Compare(mappedActualJson, expectedStateJson, comparisonRuleInfo.Parameters);

        areEqual.Should().BeTrue($"Hierarchical comparison failed for {rootEntityName} with ID {idToFind}.");
    }

    private async Task<object?> QueryHierarchy(string rootEntityName, object id, List<string> includePaths)
    {
        var entityType = _dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.ClrType.Name.Equals(rootEntityName, System.StringComparison.OrdinalIgnoreCase))
            ?? throw new System.ArgumentException($"Entity '{rootEntityName}' not found in the DbContext.");

        // 1. Get the generic Set<T>() method and invoke it to get a strongly-typed IQueryable<T>.
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), System.Type.EmptyTypes)
                                         .MakeGenericMethod(entityType.ClrType);
        var dbSet = setMethod.Invoke(_dbContext, null);
        var query = (IQueryable)dbSet;

        // 2. Find and apply the generic Include<TEntity>(source, path) method.
        var includeMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType == typeof(string))
            .MakeGenericMethod(entityType.ClrType);

        foreach (var path in includePaths)
        {
            query = (IQueryable)includeMethod.Invoke(null, new object[] { query, path });
        }

        // 3. Find and apply the generic AsNoTracking<TEntity>(source) method.
        var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethod(nameof(EntityFrameworkQueryableExtensions.AsNoTracking), BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(entityType.ClrType);

        query = (IQueryable)asNoTrackingMethod.Invoke(null, new object[] { query });


        // 4. Build the dynamic WHERE clause.
        var pkProperty = entityType.FindPrimaryKey().Properties.First();
        var parameter = Expression.Parameter(entityType.ClrType, "p");
        var pkPropertyExpression = Expression.Property(parameter, pkProperty.Name);
        var convertedId = System.Convert.ChangeType(id, pkProperty.ClrType);
        var idValueExpression = Expression.Constant(convertedId);
        var equalsExpression = Expression.Equal(pkPropertyExpression, idValueExpression);
        var lambda = Expression.Lambda(equalsExpression, parameter);

        // 5. Find and invoke the generic FirstOrDefaultAsync<T> method.
        var firstOrDefaultMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.FirstOrDefaultAsync) && m.GetParameters().Length == 3)
            .MakeGenericMethod(entityType.ClrType);

        var task = (Task)firstOrDefaultMethod.Invoke(null, new object[] { query, lambda, CancellationToken.None });
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