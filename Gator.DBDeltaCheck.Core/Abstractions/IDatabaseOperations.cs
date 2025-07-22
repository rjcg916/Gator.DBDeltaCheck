using System.Data;

namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IDatabaseOperations
{
    IDbConnection GetDbConnection();
    Task<IEnumerable<T>> GetTableStateAsync<T>(string tableName) where T : class;
    Task SeedTableAsync(string tableName, string jsonContent);
    Task SeedCollectionAsync(string rootTableName, string jsonContent);
}