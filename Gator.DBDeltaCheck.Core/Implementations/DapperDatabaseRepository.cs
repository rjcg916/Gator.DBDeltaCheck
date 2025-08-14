using System.Data.Common;
using System.Text;
using Dapper;
using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.Data.SqlClient;

namespace Gator.DBDeltaCheck.Core.Implementations;

public class DapperDatabaseRepository(string connectionString) : IDatabaseRepository
{
    public DbConnection GetDbConnection()
    {
        return new SqlConnection(connectionString);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null) where T : class
    {
        await using var connection = GetDbConnection();
        return await connection.QueryAsync<T>(sql, param);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        await using var connection = GetDbConnection();

        // 1. Execute the query to get a potentially null object.
        var result = await connection.ExecuteScalarAsync<object>(sql, param);

        // 2. Check for null or DBNull, which Dapper might return.
        if (result == null || result == DBNull.Value)
        {
            // Return the default value for the type T (e.g., 0 for int, null for string).
            return default;
        }

        // 3. If the result is not null, safely convert it to the target type T.
        return (T)Convert.ChangeType(result, typeof(T));
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        await using var connection = GetDbConnection();
        return await connection.ExecuteAsync(sql, param);
    }

    public async Task<int> InsertRecordAsync(string tableName, object data, bool allowIdentityInsert = false)
    {
        var recordData = (IDictionary<string, object>)data;

        var propertyNames = recordData.Keys;
        if (!propertyNames.Any())
            throw new ArgumentException("The data dictionary provided has no keys to insert.", nameof(data));

        var columnNames = string.Join(", ", propertyNames);
        var valueParameters = string.Join(", ", propertyNames.Select(p => "@" + p));

        var sqlBuilder = new StringBuilder();

        if (allowIdentityInsert) sqlBuilder.AppendLine($"SET IDENTITY_INSERT {tableName} ON; ");

        sqlBuilder.AppendLine($"INSERT INTO {tableName} ({columnNames}) VALUES ({valueParameters});");


        await using var connection = GetDbConnection();

        return await connection.ExecuteAsync(sqlBuilder.ToString(), data);
    }

    public async Task<T> InsertRecordAndGetIdAsync<T>(string tableName, object data, string idColumnName,
        bool allowIdentityInsert = false)
    {
        var recordData = (IDictionary<string, object>)data;

        var propertyNames = recordData.Keys;
        if (!propertyNames.Any())
            throw new ArgumentException("The data dictionary provided has no keys to insert.", nameof(data));

        var columnNames = string.Join(", ", propertyNames);
        var valueParameters = string.Join(", ", propertyNames.Select(p => "@" + p));

        var sqlBuilder = new StringBuilder();

        if (allowIdentityInsert) sqlBuilder.AppendLine($"SET IDENTITY_INSERT {tableName} ON;");

        sqlBuilder.AppendLine($"INSERT INTO {tableName} ({columnNames}) ");
        sqlBuilder.Append($"OUTPUT INSERTED.{idColumnName} ");
        sqlBuilder.Append($"VALUES ({valueParameters});");

        await using var connection = GetDbConnection();
        return await connection.QuerySingleAsync<T>(sqlBuilder.ToString(), data);
    }
}