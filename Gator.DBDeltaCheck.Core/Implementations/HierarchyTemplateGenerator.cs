using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Gator.DBDeltaCheck.Core.Implementations;

/// <summary>
/// A service that generates a hierarchical JSON template and a companion data map
/// by inspecting the DbContext schema, starting from a root table.
/// </summary>
public class HierarchyTemplateGenerator(DbContext dbContext)
{
    /// <summary>
    /// Generates a "Test Kit" containing a hierarchical template and a corresponding data map.
    /// and separate data templates for any lookup tables.
    /// </summary>
    public (JObject Template, DataMap Map, Dictionary<string, JArray> LookupTemplates) GenerateTestKit(string rootTableName)
    {
        var rootEntityType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName()?.Equals(rootTableName, System.StringComparison.OrdinalIgnoreCase) ?? false)
            ?? throw new System.ArgumentException($"Table '{rootTableName}' not found in the DbContext model.");

        var dataMap = new DataMap { Tables = new List<TableMap>() };
        var template = BuildNode(rootEntityType, dataMap, new HashSet<string>(), null);

        var lookupTemplates = new Dictionary<string, JArray>();
        var allEntityTypes = dbContext.Model.GetEntityTypes();

        var lookupTableNames = dataMap.Tables
            .SelectMany(t => t.Lookups)
            .Select(l => l.LookupTable)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var tableName in lookupTableNames)
        {
            var entityType = allEntityTypes
                .FirstOrDefault(e => e.GetTableName()?.Equals(tableName, System.StringComparison.OrdinalIgnoreCase) ?? false);

            if (entityType != null)
            {
                var lookupTemplateObject = GenerateFlatTemplate(entityType);
                lookupTemplates[tableName] = new JArray(lookupTemplateObject);
            }
        }

        return (template, dataMap, lookupTemplates);
    }

    /// <summary>
    /// Recursively builds a JObject node for an entity, including its parent and child relationships.
    /// </summary>
    private static JObject BuildNode(IEntityType entityType, DataMap dataMap, ISet<string> visitedNavigations, IEntityType? parentEntityType, int currentDepth = 0)
    {
        var node = new JObject();
        var tableName = entityType.GetTableName()!;
        var tableMap = new TableMap { Name = tableName, Lookups = new List<LookupRule>() };

        // 1. Add scalar properties with annotated placeholders.
        foreach (var property in entityType.GetProperties().Where(p => !p.IsForeignKey() && !p.IsPrimaryKey()))
        {

            bool hasDefault = property.GetDefaultValue() != null || !string.IsNullOrEmpty(property.GetDefaultValueSql());

            // If the property is optional OR has a default, comment it out.
            var propertyName = (property.IsNullable || hasDefault) ? $"// {property.Name}" : property.Name;

            node[propertyName] = GeneratePlaceholder(property);
        }

        // 2. Add PARENT/LOOKUP relationships.
        foreach (var foreignKey in entityType.GetForeignKeys())
        {
            var principalEntityType = foreignKey.PrincipalEntityType;
            if (principalEntityType == parentEntityType) continue;

            var navigation = foreignKey.DependentToPrincipal;
            if (navigation == null) continue;

            var bestLookupColumn = FindBestLookupColumn(principalEntityType);
            var principalPkName = principalEntityType.FindPrimaryKey()!.Properties.First().GetColumnName();

            // If the best lookup column is just the primary key, the table is too complex
            // to be treated as a simple lookup.
            if (bestLookupColumn.Equals(principalPkName, System.StringComparison.OrdinalIgnoreCase))
            {
                // Instead of a friendly lookup, add a placeholder for the actual foreign key property.
                // It now checks if the foreign key property itself is nullable.
                var fkProperty = foreignKey.Properties.First();
                var propertyName = fkProperty.IsNullable ? $"// {fkProperty.Name}" : fkProperty.Name;
                var requiredText = fkProperty.IsNullable ? "optional ID" : "required ID";

                node[propertyName] = $"TODO: Add {requiredText} for {principalEntityType.GetTableName()}";

            }
            else
            {
                // Otherwise, it's a simple lookup table, so create the property and map rule.
                var dataPropertyName = navigation.Name;
                node[dataPropertyName] = $"TODO: Add lookup value for {dataPropertyName} (e.g., a {bestLookupColumn})";

                tableMap.Lookups.Add(new LookupRule
                {
                    DataProperty = dataPropertyName,
                    LookupTable = principalEntityType.GetTableName()!,
                    LookupValueColumn = bestLookupColumn
                });
            }
        }

        // 3. Add CHILD relationships, but only if we are at the root level (depth 0).
        if (currentDepth == 0)
        {
            foreach (var navigation in entityType.GetNavigations().Where(n => n.IsCollection))
            {
                if (visitedNavigations.Contains(navigation.Name)) continue;
                visitedNavigations.Add(navigation.Name);

                var childEntityType = navigation.TargetEntityType;
                // In the recursive call, increment the depth.
                var childNode = BuildNode(childEntityType, dataMap, visitedNavigations, entityType, currentDepth + 1);
                node[navigation.Name] = new JArray(childNode);
            }
        }

        // Add the completed map for this table to the main data map.
        if (tableMap.Lookups.Any())
        {
            dataMap.Tables.Add(tableMap);
        }

        return node;
    }

    /// <summary>
    /// Generates a flat JObject template for a single entity type, including only its scalar properties.
    /// </summary>
    private static JObject GenerateFlatTemplate(IEntityType entityType)
    {
        var node = new JObject();
        // Add scalar properties, excluding foreign keys.
        foreach (var property in entityType.GetProperties().Where(p => !p.IsForeignKey()))
        {
            // Exclude auto-generated primary keys, but include user-provided (natural) keys.
            if (property.IsPrimaryKey() && property.ValueGenerated != ValueGenerated.Never)
            {
                continue;
            }

            bool hasDefault = property.GetDefaultValue() != null || !string.IsNullOrEmpty(property.GetDefaultValueSql());

            // If the property is optional OR has a default, comment it out.
            // A user-provided PK is never optional, so we make it a required property in the template.
            var propertyName = (property.IsNullable || hasDefault) && !property.IsPrimaryKey()
                ? $"// {property.Name}"
                : property.Name;

            node[propertyName] = GeneratePlaceholder(property);
        }
        return node;
    }

    private static string GeneratePlaceholder(IProperty property)
    {
        var sb = new StringBuilder("TODO: ");
        // Get the underlying type if it's nullable, otherwise use the type itself for a cleaner name.
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        sb.Append(type.Name);
        var maxLength = property.GetMaxLength();
        if (maxLength.HasValue)
        {
            sb.Append($"({maxLength})");
        }
        sb.Append(property.IsNullable ? ", optional" : ", required");

        var defaultValue = property.GetDefaultValue();
        var defaultValueSql = property.GetDefaultValueSql();

        if (defaultValue != null)
        {
            sb.Append($", default: {defaultValue}");
        }
        else if (defaultValueSql != null)
        {
            // Check for the specific SQL Server default pattern for strings.
            if (defaultValueSql.Equals("((0))", StringComparison.Ordinal))
            {
                sb.Append(", default: '0'");
            }
            else
            {
                sb.Append($", default SQL: {defaultValueSql}");
            }
        }
        return sb.ToString();
    }

    private static string FindBestLookupColumn(IEntityType entityType)
    {
        var pkProperty = entityType.FindPrimaryKey()!.Properties.First();

        // If the entity has any outgoing foreign keys, it's not a simple lookup table.
        // In this case, the safest bet is to use its primary key for lookups.
        if (entityType.GetForeignKeys().Any())
        {
            return pkProperty.GetColumnName();
        }

        var priorityNames = new[] { "Name", "Title", "Description", "Code", "Email", "Key" };

        // Find the best candidate property by ordering them based on a set of rules.
        // Rule 1: Prefer properties with "friendly" names, respecting the order in priorityNames.
        // Rule 2: Prefer properties that are required (not nullable) over optional ones.
        var bestProperty = entityType.GetProperties()
            .Where(p => p.ClrType == typeof(string) && !p.IsPrimaryKey())
            .OrderBy(p => {
                int index = Array.FindIndex(priorityNames, name => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                return index == -1 ? priorityNames.Length : index; // Lower index = higher priority.
            })
            .ThenBy(p => p.IsNullable) // false (required) comes before true (optional)
            .FirstOrDefault();

        // If a suitable string property is found, use it. Otherwise, fall back to the primary key.
        return bestProperty?.GetColumnName() ?? pkProperty.GetColumnName();
    }
}