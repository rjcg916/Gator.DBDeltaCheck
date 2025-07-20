namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IDbSchemaService
{
    Task<string> GetPrimaryKeyColumnNameAsync(string tableName);
    Task<Dictionary<string, string>> GetForeignKeyRelationshipsAsync(string tableName);
    Task<object> GetLookupTableIdAsync(string lookupTableName, string valueColumn, string displayValue);
}
