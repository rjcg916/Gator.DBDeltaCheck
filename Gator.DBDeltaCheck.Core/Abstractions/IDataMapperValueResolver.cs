namespace Gator.DBDeltaCheck.Core.Abstractions;

/// <summary>
/// A specialized service responsible for executing the database lookups
/// required by the DataMapper.
/// </summary>
public interface IDataMapperValueResolver
{
    /// <summary>
    /// Finds the primary key ID for a record in a lookup table.
    /// </summary>
    Task<object?> ResolveToId(string lookupTable, string lookupValueColumn, object displayValue);

    /// <summary>
    /// Finds the "friendly" display value for a record given its primary key ID.
    /// </summary>
    Task<object?> ResolveToFriendlyValue(string lookupTable, string lookupValueColumn, object idValue);
}
