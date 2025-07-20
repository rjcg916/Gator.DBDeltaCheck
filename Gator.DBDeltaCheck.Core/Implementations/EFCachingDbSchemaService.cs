using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class EFCachingDbSchemaService : IDbSchemaService
{
    private readonly DbContext _dbContext;
    private readonly IDatabaseRepository _dbRepository;
    private static readonly ConcurrentDictionary<string, string> _primaryKeyCache = new();
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _foreignKeyCache = new();

    public EFCachingDbSchemaService(DbContext dbContext, IDatabaseRepository dbRepository)
    {
        _dbContext = dbContext;
        _dbRepository = dbRepository;
    }

    public async Task<string> GetPrimaryKeyColumnNameAsync(string tableName)
    {
        if (_primaryKeyCache.TryGetValue(tableName, out var pkColumn))
        {
            return pkColumn;
        }

        var entityType =  _dbContext.Model.FindEntityType(tableName);
        if (entityType == null) throw new ArgumentException($"Table {tableName} not found in model.");

        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey == null) throw new InvalidOperationException($"No primary key defined for table {tableName}.");

        var result = primaryKey.Properties[0].Name;
        _primaryKeyCache.TryAdd(tableName, result);
        return result;
    }

    public async Task<Dictionary<string, string>> GetForeignKeyRelationshipsAsync(string tableName)
    {
        if (_foreignKeyCache.TryGetValue(tableName, out var fkRelations))
        {
            return fkRelations;
        }

        var entityType = _dbContext.Model.FindEntityType(tableName);
        if (entityType == null) throw new ArgumentException($"Table {tableName} not found in model.");

        var relationships = entityType.GetForeignKeys()
            .ToDictionary(fk => fk.Properties[0].Name, fk => fk.PrincipalEntityType.Name);

        _foreignKeyCache.TryAdd(tableName, relationships);
        return relationships;
    }

    public async Task<object> GetLookupTableIdAsync(string lookupTableName, string valueColumn, string displayValue)
    {
        var pkColumn = await GetPrimaryKeyColumnNameAsync(lookupTableName);
        var sql = $"SELECT {pkColumn} FROM {lookupTableName} WHERE {valueColumn} = @displayValue";
        return await _dbRepository.ExecuteScalarAsync<object>(sql, new { displayValue });
    }
}
