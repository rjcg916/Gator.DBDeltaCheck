using Gator.DBDeltaCheck.Core.Abstractions;
using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class HierarchyScaffolder(DbContext dbContext, IDataMapper dataMapper)
{
    // The main method is updated to accept the parsed template JObject.
    public async Task<(List<JObject> HierarchicalData, Dictionary<string, JArray> LookupData)> Scaffold(
        string rootTableName,
        IEnumerable<object> rootKeys,
        DataMap dataMap,
        JObject rootTemplate,
        Dictionary<string, JArray> lookupTemplates,
        bool excludeDefaults)
    {
        var rootEntityType = FindEntityType(rootTableName);
        var allHierarchies = new List<JObject>();

        var serializerSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        // Generate the include paths ONCE from the template's structure.
        var includes = GenerateIncludePathsFromTemplate(rootEntityType, rootTemplate, null);

        // Ensure we only process each unique root key once to prevent duplicate output.
        var distinctRootKeys = rootKeys.Distinct().ToList();

        foreach (var key in distinctRootKeys)
        {
            // Pass the generated include paths to the query method.
            var rootObject = await QueryHierarchy(rootEntityType, key, includes);
            if (rootObject == null) continue;

            var rawJson = JsonConvert.SerializeObject(rootObject, serializerSettings);
            var mappedJson = await dataMapper.MapToFriendlyState(rawJson, dataMap, rootTableName, excludeDefaults);
            allHierarchies.Add(JObject.Parse(mappedJson));
        }

        // Scaffold all lookup tables defined in the data map.
        var lookupData = new Dictionary<string, JArray>();
        var lookupTableNames = dataMap.Tables
            .SelectMany(t => t.Lookups)
            .Select(l => l.LookupTable)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var tableName in lookupTableNames)
        {
            if (lookupTemplates.TryGetValue(tableName, out var lookupTemplateArray) &&
                lookupTemplateArray.FirstOrDefault() is JObject lookupTemplateObject)
            {
                var lookupJsonArray = await ScaffoldLookupTable(tableName, lookupTemplateObject);
                if (lookupJsonArray.Any())
                    lookupData[tableName] = lookupJsonArray;
            }
        }

        return (allHierarchies, lookupData);
    }

    private async Task<object?> QueryHierarchy(IEntityType entityType, object id, List<string> includePaths)
    {
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), System.Type.EmptyTypes)!
                                         .MakeGenericMethod(entityType.ClrType);
        var dbSet = setMethod.Invoke(dbContext, null)!;
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
    private static List<string> GenerateIncludePathsFromTemplate(IEntityType entityType, JObject templateNode, INavigation? parentNavigation)
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

    /// <summary>
    /// Scaffolds all rows from a lookup table, including only the columns
    /// that are not commented out in the provided template.
    /// </summary>
    private async Task<JArray> ScaffoldLookupTable(string tableName, JObject template)
    {
        // 1. Get the set of columns to include from the template.
        var columnsToInclude = template.Properties()
            .Where(p => !p.Name.StartsWith("//"))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columnsToInclude.Any()) return new JArray();

        // 2. Query all data from the database table.
        var entityType = FindEntityType(tableName);
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(entityType.ClrType);
        var dbSet = setMethod.Invoke(dbContext, null)!;

        var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType.ClrType);

        var task = (Task)toListAsyncMethod.Invoke(null, new object[] { dbSet, default(CancellationToken) })!;
        await task;
        var results = (IEnumerable<object>)((dynamic)task).Result;

        // 3. Build the JArray, projecting only the desired properties.
        var jsonArray = new JArray();
        var propertiesToMap = entityType.GetProperties()
            .Where(p => columnsToInclude.Contains(p.Name) && p.PropertyInfo != null)
            .Select(p => p.PropertyInfo!)
            .ToList();

        foreach (var entity in results)
        {
            var entityJObject = new JObject();
            foreach (var propInfo in propertiesToMap)
            {
                entityJObject[propInfo.Name] = JToken.FromObject(propInfo.GetValue(entity) ?? JValue.CreateNull());
            }
            jsonArray.Add(entityJObject);
        }
        return jsonArray;
    }
    private IEntityType FindEntityType(string tableName) =>
        dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName()?.Equals(tableName, System.StringComparison.OrdinalIgnoreCase) ?? false)
        ?? throw new System.ArgumentException($"Table '{tableName}' not found in the DbContext model.");
}