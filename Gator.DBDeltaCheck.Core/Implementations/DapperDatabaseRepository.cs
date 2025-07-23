using Dapper;
using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Text;


namespace Gator.DBDeltaCheck.Core.Implementations;


public class DapperDatabaseRepository : IDatabaseRepository
{
    private readonly string _connectionString;

    public DapperDatabaseRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbConnection GetDbConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null) where T : class
    {
        using var connection = GetDbConnection();
        return await connection.QueryAsync<T>(sql, param);
    }

    public async Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        using var connection = GetDbConnection();
        return await connection.ExecuteScalarAsync<T>(sql, param);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var connection = GetDbConnection();
        return await connection.ExecuteAsync(sql, param);
    }

    public async Task<int> InsertRecordAsync(string tableName, object data)
    {
        var properties = data.GetType().GetProperties().Select(p => p.Name);
        var sql = $"INSERT INTO {tableName} ({string.Join(", ", properties)}) VALUES (@{string.Join(", @", properties)});";

        using var connection = GetDbConnection();
        return await connection.ExecuteAsync(sql, data);
    }

    public async Task<T> InsertRecordAndGetIdAsync<T>(string tableName, object data, string idColumnName)
    {
        var properties = data.GetType().GetProperties().Select(p => p.Name);
        var sql = new StringBuilder($"INSERT INTO {tableName} ({string.Join(", ", properties)}) ");
        sql.Append($"OUTPUT INSERTED.{idColumnName} ");
        sql.Append($"VALUES (@{string.Join(", @", properties)});");

        using var connection = GetDbConnection();
        return await connection.QuerySingleAsync<T>(sql.ToString(), data);
    }
}