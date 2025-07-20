namespace Gator.DBDeltaCheck.Core.Abstractions;

public interface IDatabaseRepository
{
    System.Data.IDbConnection GetDbConnection();
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);
    Task<int> ExecuteAsync(string sql, object? param = null);
    Task<T> ExecuteScalarAsync<T>(string sql, T value);
}
