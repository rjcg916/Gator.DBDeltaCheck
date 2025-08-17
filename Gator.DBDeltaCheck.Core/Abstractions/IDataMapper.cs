using Gator.DBDeltaCheck.Core.Models;

namespace Gator.DBDeltaCheck.Core.Abstractions;

/// <summary>
/// A unified service for transforming data between its raw database state (with IDs)
/// and a "friendly" state (with human-readable values), using a data map.
/// </summary>
public interface IDataMapper
{
    /// <summary>Takes a "friendly" JSON template and resolves its lookups to real database IDs.
    /// </summary>
    Task<string> ResolveToDbState(string templateJson, DataMap dataMap, string tableName);

    /// <summary>
    /// Takes raw JSON from the database and maps its foreign keys to "friendly" values.
    /// </summary>
    Task<string> MapToFriendlyState(string dbJson, DataMap dataMap, string tableName, bool excludeDefaults = false);
}
