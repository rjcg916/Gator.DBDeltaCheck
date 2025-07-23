namespace Gator.DBDeltaCheck.Core.Abstractions;


/// <summary>
/// Provides metadata about the database schema, discovered from the DbContext.
/// Designed to be asynchronous and cache results for performance.
/// </summary>
public interface IDbSchemaService
{
    /// <summary>
    /// Gets the name of the foreign key column in a child table that references a parent table.
    /// </summary>
    Task<string> GetForeignKeyColumnNameAsync(string childTableName, string parentTableName);

    /// <summary>
    /// Gets information about the child collections for a given parent table.
    /// </summary>
    Task<IEnumerable<ChildTableInfo>> GetChildTablesAsync(string parentTableName);

    /// <summary>
    /// Gets the primary key column name for a given table.
    /// </summary>
    Task<string> GetPrimaryKeyColumnNameAsync(string tableName);

    /// <summary>
    /// Gets the .NET Type of the primary key for a given table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <returns>The Type of the primary key (e.g., typeof(int), typeof(Guid)).</returns>
    Task<Type> GetPrimaryKeyTypeAsync(string tableName);
}

/// <summary>
/// A record to hold information about a child relationship.
/// </summary>
/// <param name="ChildTableName">The name of the child database table.</param>
/// <param name="ChildCollectionName">The name of the collection property on the parent entity (e.g., "ProductReviews").</param>
public record ChildTableInfo(string ChildTableName, string ChildCollectionName);

