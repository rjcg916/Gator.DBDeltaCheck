namespace Gator.DBDeltaCheck.Core.Models;

/// <summary>
///     Represents a single instruction for seeding data into a database table from a JSON file.
/// </summary>
public class SeedDataInstruction
{
    /// <summary>
    ///     The name of the SQL database table to seed.
    /// </summary>
    public required string Table { get; set; }

    /// <summary>
    ///     The relative path to a JSON file containing an array of objects to insert into the table.
    /// </summary>
    public required string DataFilePath { get; set; }
}