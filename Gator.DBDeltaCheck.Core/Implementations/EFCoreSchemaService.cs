using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class EfCoreSchemaService : IDbSchemaService
{
    private readonly DbContext _dbContext;

    // Caching dictionaries to store the schema after the first discovery
    private IReadOnlyDictionary<string, List<string>>? _dependenciesCache;
    private readonly Dictionary<string, string> _primaryKeyCache = new();
    private readonly Dictionary<string, List<ChildTableInfo>> _childTableCache = new();

    public EfCoreSchemaService(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public IReadOnlyDictionary<string, List<string>> GetTableDependencies()
    {
        // Return from cache if already computed
        if (_dependenciesCache != null)
        {
            return _dependenciesCache;
        }

        var dependencies = new Dictionary<string, List<string>>();
        var entityTypes = _dbContext.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            if (tableName == null) continue;

            var foreignKeys = entityType.GetForeignKeys();
            foreach (var fk in foreignKeys)
            {
                var principalTableName = fk.PrincipalEntityType.GetTableName();
                if (principalTableName == null) continue;

                if (!dependencies.ContainsKey(tableName))
                {
                    dependencies[tableName] = new List<string>();
                }

                if (!dependencies[tableName].Contains(principalTableName))
                {
                    dependencies[tableName].Add(principalTableName);
                }
            }
        }

        // Store the result in the cache before returning
        _dependenciesCache = dependencies;
        return _dependenciesCache;
    }

    public IEnumerable<ChildTableInfo> GetChildTables(string parentTableName)
    {
        // Return from cache if already computed for this table
        if (_childTableCache.TryGetValue(parentTableName, out var cachedInfo))
        {
            return cachedInfo;
        }

        var parentEntityType = FindEntityType(parentTableName);
        var navigations = parentEntityType.GetNavigations()
          .Where(nav => nav.IsCollection)
          .Select(nav => new ChildTableInfo(
                nav.TargetEntityType.GetTableName()!,
                nav.Name))
          .ToList();

        // Store the result in the cache before returning
        _childTableCache = navigations;
        return navigations;
    }

    public string GetPrimaryKeyColumnName(string tableName)
    {
        // Return from cache if already computed for this table
        if (_primaryKeyCache.TryGetValue(tableName, out var cachedKey))
        {
            return cachedKey;
        }

        var entityType = FindEntityType(tableName);
        var primaryKey = entityType.FindPrimaryKey();

        if (primaryKey == null)
        {
            throw new InvalidOperationException($"No primary key found for table '{tableName}'.");
        }

        var pkColumnName = primaryKey.Properties.First().Name;

        // Store the result in the cache before returning
        _primaryKeyCache[tableName] = pkColumnName;
        return pkColumnName;
    }

    // Other methods like GetForeignKeyColumnName and FindEntityType remain the same...
    public string GetForeignKeyColumnName(string childTableName, string parentTableName)
    {
        // This logic is fast and doesn't necessarily need caching, but could be cached if desired.
        var childEntityType = FindEntityType(childTableName);
        var parentEntityType = FindEntityType(parentTableName);

        var foreignKey = childEntityType.GetForeignKeys()
          .FirstOrDefault(fk => fk.PrincipalEntityType == parentEntityType);

        if (foreignKey == null)
        {
            throw new InvalidOperationException($"No foreign key relationship found from '{childTableName}' to '{parentTableName}'.");
        }

        return foreignKey.Properties.First().Name;
    }

    private IEntityType FindEntityType(string tableName)
    {
        var entityType = _dbContext.Model.GetEntityTypes()
          .FirstOrDefault(et => et.GetTableName()?.Equals(tableName, StringComparison.OrdinalIgnoreCase) == true);

        if (entityType == null)
        {
            throw new InvalidOperationException($"Table '{tableName}' could not be found in the DbContext model.");
        }

        return entityType;
    }
}
