

using System.Collections.Generic;

namespace Gator.DBDeltaCheck.Core.Abstractions;

/// <summary>
/// Provides metadata about the database schema, discovered from the DbContext.
/// </summary>
public interface IDbSchemaService
{
    /// <summary>
    /// Gets a dependency graph of all tables.
    /// </summary>
    /// <returns>A dictionary where the key is a table (dependent) and the value is a list of tables it depends on (principals).</returns>
    IReadOnlyDictionary<string, List<string>> GetTableDependencies();

    /// <summary>
    /// Gets the name of the foreign key column in a child table that references a parent table.
    /// </summary>
    /// <param name="childTableName">The name of the dependent (child) table.</param>
    /// <param name="parentTableName">The name of the principal (parent) table.</param>
    /// <returns>The name of the foreign key column.</returns>
    string GetForeignKeyColumnName(string childTableName, string parentTableName);

    /// <summary>
    /// Gets information about the child collections for a given parent table.
    /// </summary>
    /// <param name="parentTableName">The name of the parent table.</param>
    /// <returns>An enumerable of objects containing child table and collection property names.</returns>
    IEnumerable<ChildTableInfo> GetChildTables(string parentTableName);

    /// <summary>
    /// Gets the primary key column name for a given table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <returns>The name of the primary key column.</returns>
    string GetPrimaryKeyColumnName(string tableName);
}

/// <summary>
/// A record to hold information about a child relationship.
/// </summary>
/// <param name="ChildTableName">The name of the child database table.</param>
/// <param name="ChildCollectionName">The name of the collection property on the parent entity (e.g., "ProductReviews").</param>
public record ChildTableInfo(string ChildTableName, string ChildCollectionName);
