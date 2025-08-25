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
    /// </summary>
    public (JObject Template, DataMap Map) GenerateTestKit(string rootTableName)
    {
        var rootEntityType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName()?.Equals(rootTableName, System.StringComparison.OrdinalIgnoreCase) ?? false)
            ?? throw new System.ArgumentException($"Table '{rootTableName}' not found in the DbContext model.");

        var dataMap = new DataMap { Tables = new List<TableMap>() };
        var template = BuildNode(rootEntityType, dataMap, new HashSet<string>(), null);

        return (template, dataMap);
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

    private static string GeneratePlaceholder(IProperty property)
    {
        var sb = new StringBuilder("TODO: ");
        sb.Append(property.ClrType.Name);
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
            return pkProperty.GetColumnName()!;
        }

        // Rule 1: Prioritize common "friendly" names that are strings.
        var priorityNames = new[] { "Name", "Title", "Description", "Key", "Code", "Email" };
        foreach (var name in priorityNames)
        {
            var property = entityType.GetProperties()
                .FirstOrDefault(p => p.Name.Contains(name, System.StringComparison.OrdinalIgnoreCase) && p.ClrType == typeof(string));

            if (property != null)
            {
                return property.GetColumnName()!;
            }
        }

        // Rule 2: If no priority names are found, find the FIRST string property
        // that is NOT the primary key.
        var firstStringProperty = entityType.GetProperties()
            .FirstOrDefault(p => p.ClrType == typeof(string) && !p.Name.Equals(pkProperty.Name, System.StringComparison.OrdinalIgnoreCase));

        if (firstStringProperty != null)
        {
            return firstStringProperty.GetColumnName()!;
        }

        // Rule 3: As a last resort, fall back to the primary key.
        return entityType.FindPrimaryKey()!.Properties.First().GetColumnName()!;
    }
}