using System.Data;

namespace Gator.DBDeltaCheck.Core.Abstractions;

using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

public interface IDatabaseRepository
{
    /// <summary>
    /// Gets a raw, open database connection for tools like Respawner.
    /// </summary>
    IDbConnection GetDbConnection();

    /// <summary>
    /// Executes a query and returns a collection of results.
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);

    /// <summary>
    /// Executes a query that returns a single value (e.g., a count, or a single ID).
    /// </summary>
    Task<T> ExecuteScalarAsync<T>(string sql, object? param = null);

    /// <summary>
    /// Executes a command that does not return a result (e.g., UPDATE, DELETE).
    /// </summary>
    Task<int> ExecuteAsync(string sql, object? param = null);

    /// <summary>
    /// Inserts a single record into a table. Does not return the new ID.
    /// </summary>
    Task<int> InsertRecordAsync(string tableName, object data);

    /// <summary>
    /// Inserts a single record and returns its newly generated primary key.
    /// Essential for hierarchical seeding.
    /// </summary>
    /// <typeparam name="T">The data type of the primary key (e.g., int, Guid).</typeparam>
    Task<T> InsertRecordAndGetIdAsync<T>(string tableName, object data, string idColumnName);
}