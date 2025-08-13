using Gator.DBDeltaCheck.Core.Models;

namespace Gator.DBDeltaCheck.Core.Abstractions;
/// <summary>
/// Defines a service that transforms raw database results into a "friendly"
/// format using a data map, replacing foreign key IDs with business values.
/// </summary>
public interface IActualStateMapper
{
    /// <summary>
    /// Takes a raw JSON string of database results and resolves its foreign keys.
    /// </summary>
    /// <param name="rawActualStateJson">The JSON representation of the data from the database.</param>
    /// <param name="dataMap">The loaded data map containing lookup rules.</param>
    /// <param name="tableName">The name of the table the data belongs to.</param>
    /// <returns>A JSON string with foreign key IDs replaced by their display values.</returns>
    Task<string> Map(string rawActualStateJson, DataMap dataMap, string tableName);
}