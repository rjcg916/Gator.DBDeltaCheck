using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Models;
/// <summary>
/// Defines a hierarchical data seeding instruction, including child entities and lookups.
/// </summary>
public class HierarchicalSeedInstruction
{
    /// <summary>
    /// The name of the root table to insert data into.
    /// </summary>

    public string RootTable { get; set; }

    /// <summary>
    /// The JSON data for the root table. Can be an array of objects or a single object.
    /// </summary>

    public JToken Data { get; set; }

    /// <summary>
    /// A list of instructions for resolving foreign key values from lookup tables.
    /// </summary>

    public List<LookupInstruction> Lookups { get; set; } = new List<LookupInstruction>();
}
