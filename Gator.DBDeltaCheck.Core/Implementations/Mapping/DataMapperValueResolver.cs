using Gator.DBDeltaCheck.Core.Abstractions;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;

public class DataMapperValueResolver(IDatabaseRepository repository, IDbSchemaService schemaService)
    : IDataMapperValueResolver
{
    public async Task<object?> ResolveToId(string lookupTable, string lookupValueColumn, object displayValue)
    {
        var pkColumn = await schemaService.GetPrimaryKeyColumnNameAsync(lookupTable);
        var sql = $"SELECT {pkColumn} FROM {lookupTable} WHERE {lookupValueColumn} = @displayValue";
        var resolvedId = await repository.ExecuteScalarAsync<object>(sql, new { displayValue });

        if (resolvedId == null)
        {
            throw new InvalidOperationException($"Lookup failed: Could not find record in '{lookupTable}' where '{lookupValueColumn}' is '{displayValue}'.");
        }
        return resolvedId;
    }

    public async Task<object?> ResolveToFriendlyValue(string lookupTable, string lookupValueColumn, object idValue)
    {
        var pkColumn = await schemaService.GetPrimaryKeyColumnNameAsync(lookupTable);
        var sql = $"SELECT {lookupValueColumn} FROM {lookupTable} WHERE {pkColumn} = @idValue";
        return await repository.ExecuteScalarAsync<object>(sql, new { idValue });
    }
}
