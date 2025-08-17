using System.Collections.Concurrent;
using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Gator.DBDeltaCheck.Core.Implementations;

/// <summary>
/// Implements IDbSchemaService using Entity Framework Core's metadata model.
/// This service is designed to be highly performant by caching schema information
/// after the first discovery, avoiding repeated lookups during a test run.
/// </summary>
public class EFCachingDbSchemaService(DbContext dbContext) : IDbSchemaService
{
    // --- Caches to prevent re-calculating schema on every test run ---
    private static readonly ConcurrentDictionary<string, string> PrimaryKeyNameCache = new();
    private static readonly ConcurrentDictionary<string, Type> PrimaryKeyTypeCache = new();
    private static readonly ConcurrentDictionary<string, IEnumerable<ChildTableInfo>> ChildTableCache = new();
    private static readonly ConcurrentDictionary<string, string> ForeignKeyCache = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> DefaultsCache = new();

    private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<string> GetPrimaryKeyColumnNameAsync(string tableName)
    {
        if (PrimaryKeyNameCache.TryGetValue(tableName, out var pkColumn)) return Task.FromResult(pkColumn);

        var entityType = FindEntityType(tableName);
        var primaryKey = entityType.FindPrimaryKey()
                         ?? throw new InvalidOperationException($"No primary key defined for table {tableName}.");

        var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        var result = primaryKey.Properties[0].GetColumnName(storeObject);
        PrimaryKeyNameCache.TryAdd(tableName, result);
        return Task.FromResult(result);
    }

    public Task<Type> GetPrimaryKeyTypeAsync(string tableName)
    {
        if (PrimaryKeyTypeCache.TryGetValue(tableName, out var pkType)) return Task.FromResult(pkType);

        var entityType = FindEntityType(tableName);
        var primaryKey = entityType.FindPrimaryKey()
                         ?? throw new InvalidOperationException($"No primary key defined for table {tableName}.");

        var result = primaryKey.Properties[0].ClrType;
        PrimaryKeyTypeCache.TryAdd(tableName, result);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<ChildTableInfo>> GetChildTablesAsync(string parentTableName)
    {
        if (ChildTableCache.TryGetValue(parentTableName, out var childTables)) return Task.FromResult(childTables);

        var parentEntityType = FindEntityType(parentTableName);
        var navigations = parentEntityType.GetNavigations()
            .Where(n => n.IsCollection)
            .Select(n => new ChildTableInfo(
                n.TargetEntityType.GetTableName()!,
                n.Name
            )).ToList();

        ChildTableCache.TryAdd(parentTableName, navigations);
        return Task.FromResult<IEnumerable<ChildTableInfo>>(navigations);
    }

    public Task<string> GetForeignKeyColumnNameAsync(string childTableName, string parentTableName)
    {
        var cacheKey = $"{childTableName}:{parentTableName}";
        if (ForeignKeyCache.TryGetValue(cacheKey, out var fkColumn)) return Task.FromResult(fkColumn);

        var childEntityType = FindEntityType(childTableName);
        var parentEntityType = FindEntityType(parentTableName);

        var foreignKey = childEntityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType == parentEntityType)
            ?? throw new InvalidOperationException($"Could not find a foreign key from '{childTableName}' to '{parentTableName}'.");

        var storeObject = StoreObjectIdentifier.Table(childTableName, childEntityType.GetSchema());
        var result = foreignKey.Properties[0].GetColumnName(storeObject);

        ForeignKeyCache.TryAdd(cacheKey, result);
        return Task.FromResult(result);
    }

    public Task<HashSet<string>> GetPropertyNamesWithDefaultsAsync(string tableName)
    {
        
        if (DefaultsCache.TryGetValue(tableName, out var cachedDefaults))
        {
            return Task.FromResult(cachedDefaults);
        }

        var entityType = FindEntityType(tableName);
        var propertiesWithDefaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in entityType.GetProperties())
        {
            if (property.GetDefaultValue() != null || !string.IsNullOrEmpty(property.GetDefaultValueSql()))
            {
                propertiesWithDefaults.Add(property.Name);
            }
        }

        DefaultsCache.TryAdd(tableName, propertiesWithDefaults);
        return Task.FromResult(propertiesWithDefaults);
    }

    private IEntityType FindEntityType(string tableName)
    {
        var entityType = _dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName()?.Equals(tableName, StringComparison.OrdinalIgnoreCase) ?? false);

        return entityType ??
               throw new ArgumentException($"Table '{tableName}' could not be found in the DbContext model. Ensure it's a mapped entity.");
    }
}
