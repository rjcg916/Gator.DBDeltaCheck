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

    public async Task<int> InsertRecordAsync(string tableName, object data, bool allowIdentityInsert = false)
    {

        var recordData = (IDictionary<string, object>)data;

        var propertyNames = recordData.Keys;
        if (!propertyNames.Any())
        {
            throw new ArgumentException("The data dictionary provided has no keys to insert.", nameof(data));
        }

        var columnNames = string.Join(", ", propertyNames);
        var valueParameters = string.Join(", ", propertyNames.Select(p => "@" + p));

        var sqlBuilder = new StringBuilder();

        if (allowIdentityInsert)
        {
            sqlBuilder.AppendLine($"SET IDENTITY_INSERT {tableName} ON; ");
        }

        sqlBuilder.AppendLine($"INSERT INTO {tableName} ({columnNames}) VALUES ({valueParameters});"); 


        using var connection = GetDbConnection();

        return await connection.ExecuteAsync(sqlBuilder.ToString(), data);
    }

    public async Task<T> InsertRecordAndGetIdAsync<T>(string tableName, object data, string idColumnName, bool allowIdentityInsert = false)
    {
        var recordData = (IDictionary<string, object>)data;

        var propertyNames = recordData.Keys;
        if (!propertyNames.Any())
        {
            throw new ArgumentException("The data dictionary provided has no keys to insert.", nameof(data));
        }

        var columnNames = string.Join(", ", propertyNames);
        var valueParameters = string.Join(", ", propertyNames.Select(p => "@" + p));

        var sqlBuilder = new StringBuilder();
        
       
        if (allowIdentityInsert)
        {
            sqlBuilder.AppendLine($"SET IDENTITY_INSERT {tableName} ON;");
        }
        sqlBuilder.AppendLine($"INSERT INTO {tableName} ({columnNames}) ");

        sqlBuilder.Append($"OUTPUT INSERTED.{idColumnName} ");
        sqlBuilder.Append($"VALUES ({valueParameters});");

        using var connection = GetDbConnection();
        return await connection.QuerySingleAsync<T>(sqlBuilder.ToString(), data);
    }
}