﻿using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;

namespace DBDeltaCheck.Core.Implementations // Or your equivalent namespace
{
    /// <summary>
    /// Implements IDbSchemaService using Entity Framework Core's metadata model.
    /// This service is designed to be highly performant by caching schema information
    /// after the first discovery, avoiding repeated lookups during a test run.
    /// </summary>
    public class EFCachingDbSchemaService : IDbSchemaService
    {
        private readonly DbContext _dbContext;

        // --- Caches to prevent re-calculating schema on every test run ---
        private static readonly ConcurrentDictionary<string, string> _primaryKeyNameCache = new();
        private static readonly ConcurrentDictionary<string, Type> _primaryKeyTypeCache = new();
        private static readonly ConcurrentDictionary<string, IEnumerable<ChildTableInfo>> _childTableCache = new();
        private static readonly ConcurrentDictionary<string, string> _foreignKeyCache = new();

        public EFCachingDbSchemaService(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Gets the primary key column name for a given table.
        /// </summary>
        public Task<string> GetPrimaryKeyColumnNameAsync(string tableName)
        {
            if (_primaryKeyNameCache.TryGetValue(tableName, out var pkColumn))
            {
                return Task.FromResult(pkColumn);
            }

            var entityType = FindEntityType(tableName);
            var primaryKey = entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException($"No primary key defined for table {tableName}.");

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            var result = primaryKey.Properties[0].GetColumnName(storeObject);
            _primaryKeyNameCache.TryAdd(tableName, result);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets the .NET Type of the primary key for a given table.
        /// </summary>
        public Task<Type> GetPrimaryKeyTypeAsync(string tableName)
        {
            if (_primaryKeyTypeCache.TryGetValue(tableName, out var pkType))
            {
                return Task.FromResult(pkType);
            }

            var entityType = FindEntityType(tableName);
            var primaryKey = entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException($"No primary key defined for table {tableName}.");

            // The ClrType property gives us the underlying .NET type (e.g., typeof(int), typeof(Guid)).
            var result = primaryKey.Properties[0].ClrType;
            _primaryKeyTypeCache.TryAdd(tableName, result);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets information about the child collections for a given parent table.
        /// </summary>
        public Task<IEnumerable<ChildTableInfo>> GetChildTablesAsync(string parentTableName)
        {
            if (_childTableCache.TryGetValue(parentTableName, out var childTables))
            {
                return Task.FromResult(childTables);
            }

            var parentEntityType = FindEntityType(parentTableName);
            var navigations = parentEntityType.GetNavigations()
                .Where(n => n.IsCollection) // We only care about one-to-many relationships
                .Select(n => new ChildTableInfo(
                    n.TargetEntityType.GetTableName()!,
                    n.Name // This is the collection property name from the C# class, e.g., "Orders"
                )).ToList();

            _childTableCache.TryAdd(parentTableName, navigations);
            return Task.FromResult<IEnumerable<ChildTableInfo>>(navigations);
        }

        /// <summary>
        /// Gets the name of the foreign key column in a child table that references a parent table.
        /// </summary>
        public Task<string> GetForeignKeyColumnNameAsync(string childTableName, string parentTableName)
        {
            var cacheKey = $"{childTableName}:{parentTableName}";
            if (_foreignKeyCache.TryGetValue(cacheKey, out var fkColumn))
            {
                return Task.FromResult(fkColumn);
            }

            var childEntityType = FindEntityType(childTableName);
            var parentEntityType = FindEntityType(parentTableName);

            var foreignKey = childEntityType.GetForeignKeys()
                .FirstOrDefault(fk => fk.PrincipalEntityType == parentEntityType)
                ?? throw new InvalidOperationException($"Could not find a foreign key from '{childTableName}' to '{parentTableName}'.");

            // FINAL CORRECTION: Use the simple GetColumnName() extension method.
            var result = foreignKey.Properties[0].GetColumnName();
            _foreignKeyCache.TryAdd(cacheKey, result);
            return Task.FromResult(result);
        }

        /// <summary>
        /// A helper method to robustly find an entity type by its table name.
        /// </summary>
        private IEntityType FindEntityType(string tableName)
        {
            // EF Core might store metadata by entity name or table name, this is more reliable.
            var entityType = _dbContext.Model.GetEntityTypes()
                .FirstOrDefault(e => e.GetTableName()?.Equals(tableName, StringComparison.OrdinalIgnoreCase) ?? false);

            return entityType ?? throw new ArgumentException($"Table '{tableName}' could not be found in the DbContext model. Ensure it's a mapped entity.");
        }
    }
}
