using Gator.DBDeltaCheck.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace DBDeltaCheck.Core.Implementations;


public class EntityFrameworkRepository : IDatabaseRepository
{
    private readonly DbContext _context;

    public EntityFrameworkRepository(DbContext context)
    {
        _context = context;
    }

    public Task<int> ExecuteAsync(string sql, object? param = null)
    {
        throw new NotImplementedException();
    }

    public Task<T> ExecuteScalarAsync<T>(string sql, T value)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the underlying database connection.
    /// </summary>
    /// <returns>An IDbConnection instance.</returns>
    public IDbConnection GetDbConnection()
    {
        
        //     return _context.Database.GetDbConnection();
    }

    /// <summary>
    /// Retrieves the state of a table asynchronously.
    /// </summary>
    /// <typeparam name="T">The entity type to map the table data to.</typeparam>
    /// <param name="tableName">The name of the database table.</param>
    /// <returns>An IEnumerable of the mapped entities.</returns>
    public async Task<IEnumerable<T>> GetTableStateAsync<T>(string tableName) where T : class
    {
        // Note: Ensure the table name is not derived from user input to prevent SQL injection.
      //  return await _context.Set<T>().FromSqlRaw($"SELECT * FROM {tableName}").ToListAsync();
    }

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Seeds a table with data from a JSON string asynchronously.
    /// </summary>
    /// <param name="tableName">The name of the database table.</param>
    /// <param name="jsonContent">The JSON string containing the data to be seeded.</param>
    /// <remarks>
    /// This method is a basic implementation and may need to be enhanced for complex scenarios,
    /// such as handling different data types and relationships. It is also important
    /// to ensure that the JSON property names match the table column names.
    /// </remarks>
    public async Task SeedTableAsync(string tableName, string jsonContent)
    {
        // Note: This implementation assumes the JSON represents an array of objects
        // where keys correspond to column names. This approach is simplified and
        // might be vulnerable to SQL injection if jsonContent is not from a trusted source.
        // For production scenarios, consider a more robust deserialization and parameterization strategy.

        var documents = JsonSerializer.Deserialize<JsonElement[]>(jsonContent);
        DbConnection connection = null;// = _context.Database.GetDbConnection();
        var hadToOpen = false;

        if (connection.State == ConnectionState.Closed)
        {
            await connection.OpenAsync();
            hadToOpen = true;
        }

        foreach (var doc in documents)
        {
            var columns = new List<string>();
            var values = new List<string>();
            var parameters = new List<DbParameter>();

            using (var command = connection.CreateCommand())
            {
                var i = 0;
                foreach (var prop in doc.EnumerateObject())
                {
                    columns.Add(prop.Name);
                    values.Add($"@p{i}");
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = $"@p{i}";
                    parameter.Value = prop.Value.ToString(); // Simplified; might need type conversion
                    command.Parameters.Add(parameter);
                    i++;
                }

                command.CommandText = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
                await command.ExecuteNonQueryAsync();
            }
        }

        if (hadToOpen)
        {
            await connection.CloseAsync();
        }
    }
}