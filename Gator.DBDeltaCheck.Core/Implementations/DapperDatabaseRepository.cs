using Dapper;
using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class DapperDatabaseRepository : IDatabaseRepository
{
    private readonly string _connectionString;

    public DapperDatabaseRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DapperDatabaseRepository()
    {
    }

    public System.Data.IDbConnection GetDbConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var connection = GetDbConnection();
        return await connection.QueryAsync<T>(sql, param);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var connection = GetDbConnection();
        return await connection.ExecuteAsync(sql, param);
    }

    public Task<T> ExecuteScalarAsync<T>(string sql, T value)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<T>> GetTableStateAsync<T>(string tableName) where T : class
    {
        throw new NotImplementedException();
    }

    public Task SeedTableAsync(string tableName, string jsonContent)
    {
        throw new NotImplementedException();
    }
}