using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Gator.DBDeltaCheck.Core.Implementations.Comparisons;

public class HierarchicalComparisonStrategy : IComparisonStrategy
{
    public string StrategyName => "HierarchicalCompare";

    private readonly DbContext _dbContext;
    private readonly IDataMapper _dataMapper;

    public HierarchicalComparisonStrategy(DbContext dbContext, IDataMapper dataMapper)
    {
        _dbContext = dbContext;
        _dataMapper = dataMapper;
    }


    public bool Compare(string? before, string after, string expected, object parameters)
    {
        throw new System.NotImplementedException("This strategy uses the ExecuteAsync method.");
    }

    public async Task<bool> ExecuteAsync(JObject parameters, Dictionary<string, object> context, DataMap? dataMap)
    {
        // 1. Get all parameters from the JSON.
        var rootEntityName = parameters["RootEntity"]?.Value<string>()
            ?? throw new System.ArgumentException("'RootEntity' is missing.");

        var findByIdToken = parameters["FindById"]?.Value<string>()
            ?? throw new System.ArgumentException("'FindById' is missing.");

        var expectedDataFile = parameters["ExpectedDataFile"]?.Value<string>()
            ?? throw new System.ArgumentException("'ExpectedDataFile' is missing.");

        var includePaths = parameters["IncludePaths"]?.ToObject<List<string>>() ?? new List<string>();
        var basePath = parameters["_basePath"]?.Value<string>() ?? Directory.GetCurrentDirectory();

        // 2. Resolve the ID from the test context.
        var idToFind = context[findByIdToken.Trim('{', '}')];

        // 3. Perform the "deep query" to get the raw actual object from the database.
        var rawActualObject = await QueryHierarchy(rootEntityName, idToFind, includePaths);
        if (rawActualObject == null)
        {
            throw new System.InvalidOperationException($"QueryHierarchy could not find an entity of type '{rootEntityName}' with ID '{idToFind}'.");
        }
        var rawActualJson = JsonConvert.SerializeObject(rawActualObject);

        // 4. Load the "friendly" expected data file.
        var expectedDataPath = Path.Combine(basePath, expectedDataFile);
        var expectedStateJson = await File.ReadAllTextAsync(expectedDataPath);

        // 5. Use the mapper to transform the raw database object into a "friendly" format.
        // If no map is provided, it will just return the raw JSON.
        var effectiveDataMap = dataMap ?? new DataMap { Tables = new List<TableMap>() };
        var mappedActualJson = await _dataMapper.MapToFriendlyState(rawActualJson, effectiveDataMap, rootEntityName);

        // 6. Compare the final, friendly JSON objects.
        var expectedToken = JToken.Parse(expectedStateJson);
        var actualToken = JToken.Parse(mappedActualJson);

        return JToken.DeepEquals(expectedToken, actualToken);
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

        var task = (Task)firstOrDefaultMethod.Invoke(null, new object[] { query, lambda, default(CancellationToken) });
        await task;

        // 6. Get the result from the completed task.
        var result = ((dynamic)task).Result;
        return result;
    }
}