using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class HierarchyScaffolder
{
    private readonly DbContext _dbContext;
    private readonly IDataMapper _dataMapper;

    public HierarchyScaffolder(DbContext dbContext, IDataMapper dataMapper)
    {
        _dbContext = dbContext;
        _dataMapper = dataMapper;
    }

    // The main method is updated to accept the parsed template JObject.
    public async Task<List<JObject>> Scaffold(
        string rootTableName,
        IEnumerable<object> rootKeys,
        DataMap dataMap,
        JObject template,
        bool excludeDefaults)
    {
        var rootEntityType = FindEntityType(rootTableName);
        var allHierarchies = new List<JObject>();

        var serializerSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        // Generate the include paths ONCE from the template's structure.
        var includes = GenerateIncludePathsFromTemplate(rootEntityType, template, null);

        foreach (var key in rootKeys)
        {
            // Pass the generated include paths to the query method.
            var rootObject = await QueryHierarchy(rootEntityType, key, includes);
            if (rootObject == null) continue;

            var rawJson = JsonConvert.SerializeObject(rootObject, serializerSettings);
            var mappedJson = await _dataMapper.MapToFriendlyState(rawJson, dataMap, rootTableName, excludeDefaults);
            allHierarchies.Add(JObject.Parse(mappedJson));
        }

        return allHierarchies;
    }

    private async Task<object?> QueryHierarchy(IEntityType entityType, object id, List<string> includePaths)
    {
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), System.Type.EmptyTypes)!
                                         .MakeGenericMethod(entityType.ClrType);
        var dbSet = setMethod.Invoke(_dbContext, null)!;
        var query = (IQueryable)dbSet;

        var includeMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType == typeof(string))
            .MakeGenericMethod(entityType.ClrType);

        foreach (var path in includePaths)
        {
            query = (IQueryable)includeMethod.Invoke(null, new object[] { query, path })!;
        }

        var pkProperty = entityType.FindPrimaryKey()!.Properties.First();
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "p");
        var pkPropertyExpression = System.Linq.Expressions.Expression.Property(parameter, pkProperty.Name);
        var convertedId = System.Convert.ChangeType(id, pkProperty.ClrType);
        var idValueExpression = System.Linq.Expressions.Expression.Constant(convertedId);
        var equalsExpression = System.Linq.Expressions.Expression.Equal(pkPropertyExpression, idValueExpression);
        var lambda = System.Linq.Expressions.Expression.Lambda(equalsExpression, parameter);

        var firstOrDefaultMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.FirstOrDefaultAsync) && m.GetParameters().Length == 3)
            .MakeGenericMethod(entityType.ClrType);

        var task = (Task)firstOrDefaultMethod.Invoke(null, new object[] { query, lambda, default(System.Threading.CancellationToken) })!;
        await task;

        var result = ((dynamic)task).Result;
        return result;
    }

    /// <summary>
    /// A helper method that recursively generates EF Core .Include() paths
    /// by analyzing the structure of the template file.
    /// </summary>
    private List<string> GenerateIncludePathsFromTemplate(IEntityType entityType, JObject templateNode, INavigation? parentNavigation)
    {
        var includes = new List<string>();
        var navs = entityType.GetNavigations().ToDictionary(n => n.Name, n => n, System.StringComparer.OrdinalIgnoreCase);

        foreach (var property in templateNode.Properties())
        {
            if (navs.TryGetValue(property.Name, out var navigation))
            {
                // Skip this navigation if it's the inverse of the one we just came from.
                if (navigation.Inverse == parentNavigation)
                {
                    continue;
                }
                // Add the top-level include (e.g., "Orders")
                includes.Add(navigation.Name);

                // Recurse to find nested includes (e.g., "OrderItems.Product")
                var childNode = (property.Value as JArray)?.FirstOrDefault() as JObject ?? property.Value as JObject;
                if (childNode == null) continue;

                var nestedIncludes = GenerateIncludePathsFromTemplate(navigation.TargetEntityType, childNode, navigation);
                foreach (var nested in nestedIncludes)
                {
                    includes.Add($"{navigation.Name}.{nested}");
                }
            }
        }
        return includes;
    }

    private IEntityType FindEntityType(string tableName) =>
        _dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName()?.Equals(tableName, System.StringComparison.OrdinalIgnoreCase) ?? false)
        ?? throw new System.ArgumentException($"Table '{tableName}' not found in the DbContext model.");
}