using Gator.DBDeltaCheck.Core.Abstractions; 
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace Gator.DBDeltaCheck.Core.Implementations 
{
    /// <summary>
    /// An implementation of IDatabaseRepository that uses Entity Framework Core.
    /// This implementation is generic and can work with any class that inherits from DbContext.
    /// </summary>
    public class EFCoreDatabaseRepository : IDatabaseRepository
    {
        private readonly DbContext _dbContext;

        /// <summary>
        /// The repository now accepts any class that inherits from DbContext.
        /// The specific instance (e.g., YourAppDbContext, IdentityDbContext)
        /// will be provided by the dependency injection container at runtime.
        /// </summary>
        public EFCoreDatabaseRepository(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public DbConnection GetDbConnection() => _dbContext.Database.GetDbConnection();
        
        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null) where T : class
        {

            var sqlParameters = new List<SqlParameter>();
            if (param != null)
            {
                foreach (var property in param.GetType().GetProperties())
                {
                    sqlParameters.Add(new SqlParameter(property.Name, property.GetValue(param) ?? DBNull.Value));
                }
            }

            return await _dbContext.Set<T>().FromSqlRaw(sql, sqlParameters.ToArray()).ToListAsync();
        }

        public async Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
        {
            // EF Core doesn't have a direct scalar execution method that returns a value from a raw query.
            // We drop down to ADO.NET, but use the connection from the DbContext to ensure it
            // participates in the same transaction.
            await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            if (param != null)
            {
                // This is a simplified parameter handling. A real implementation might need more robust logic.
                var properties = param.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    var p = command.CreateParameter();
                    p.ParameterName = $"@{prop.Name}";
                    p.Value = prop.GetValue(param) ?? DBNull.Value;
                    command.Parameters.Add(p);
                }
            }

            if (_dbContext.Database.GetDbConnection().State != ConnectionState.Open)
            {
                await _dbContext.Database.OpenConnectionAsync();
            }
            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }

        public async Task<int> ExecuteAsync(string sql, object? param = null)
        {
            return await _dbContext.Database.ExecuteSqlRawAsync(sql, param ?? Array.Empty<object>());
        }

        public async Task<int> InsertRecordAsync(string tableName, object data)
        {
            var entity = CreateAndPopulateEntity(tableName, data);
            _dbContext.Add(entity);
            return await _dbContext.SaveChangesAsync();
        }

        public async Task<T> InsertRecordAndGetIdAsync<T>(string tableName, object data, string idColumnName)
        {
            var entity = CreateAndPopulateEntity(tableName, data);
            _dbContext.Add(entity);
            await _dbContext.SaveChangesAsync();

            // After SaveChanges, EF Core populates the original entity object with the ID.
            var idProperty = entity.GetType().GetProperty(idColumnName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (idProperty == null)
            {
                throw new ArgumentException($"Property '{idColumnName}' not found on entity for table '{tableName}'.");
            }

            var idValue = idProperty.GetValue(entity);
            return (T)Convert.ChangeType(idValue, typeof(T));
        }

        /// <summary>
        /// Helper method to dynamically create an entity instance and populate it from a data object.
        /// </summary>
        private object CreateAndPopulateEntity(string tableName, object data)
        {
            // Find the EF Core entity type corresponding to the table name.
            var entityType = _dbContext.Model.GetEntityTypes()
                .FirstOrDefault(e => e.GetTableName()?.Equals(tableName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (entityType == null)
            {
                throw new ArgumentException($"Table '{tableName}' is not a known entity in the DbContext.");
            }

            // Create an instance of the C# class for this entity.
            var entity = Activator.CreateInstance(entityType.ClrType);
            var dataProperties = data.GetType().GetProperties().ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            // Use reflection to copy properties from the input 'data' object to the new entity instance.
            foreach (var entityProperty in entityType.GetProperties().Where(p => p.PropertyInfo != null))
            {
                var clrProperty = entityProperty.PropertyInfo;
                if (dataProperties.TryGetValue(clrProperty.Name, out var sourceProperty))
                {
                    var value = sourceProperty.GetValue(data);
                    clrProperty.SetValue(entity, value);
                }
            }

            return entity;
        }
    }
}
