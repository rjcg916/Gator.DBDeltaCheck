namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
///     Defines how to resolve a foreign key by looking up a value in another table.
/// </summary>
public class LookupInstruction
{
    /// <summary>
    ///     The property in the JSON data that contains the lookup value (e.g., "CategoryName").
    /// </summary>

    public required string SourceField { get; set; }

    /// <summary>
    ///     The foreign key property in the entity that needs to be populated (e.g., "CategoryId").
    /// </summary>

    public required string TargetForeignKey { get; set; }

    /// <summary>
    ///     The lookup table to query (e.g., "Categories").
    /// </summary>

    public required string LookupTable { get; set; }

    /// <summary>
    ///     The column in the lookup table to match against the sourceField value (e.g., "Name").
    /// </summary>

    public required string LookupColumn { get; set; }

    /// <summary>
    ///     The primary key column of the lookup table whose value we want to retrieve (e.g., "Id").
    /// </summary>

    public string LookupResultColumn { get; set; } = "Id"; // Default to 'Id'
}