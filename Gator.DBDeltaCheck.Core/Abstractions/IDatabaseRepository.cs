using System.Data.Common;

namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IDatabaseRepository
{
    DbConnection GetDbConnection();

    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null) where T : class;
    Task<T> ExecuteScalarAsync<T>(string sql, object? param = null);
    Task<int> ExecuteAsync(string sql, object? param = null);
    Task<int> InsertRecordAsync(string tableName, object data, bool allowIdentityInsert = false);

    Task<T> InsertRecordAndGetIdAsync<T>(string tableName, object data, string idColumnName,
        bool allowIdentityInsert = false);
}