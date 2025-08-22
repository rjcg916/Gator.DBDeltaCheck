using Gator.DBDeltaCheck.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Gator.DBDeltaCheck.Core.Implementations;

/// <summary>
/// A service that generates a hierarchical JSON template and a companion data map
/// by inspecting the DbContext schema, starting from a root table.
/// </summary>
public class HierarchyTemplateGenerator
{
    private readonly DbContext _dbContext;

    public HierarchyTemplateGenerator(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Generates a "Test Kit" containing a hierarchical template and a corresponding data map.
    /// </summary>
    public (JObject Template, DataMap Map) GenerateTestKit(string rootTableName)
    {
        var rootEntityType = _dbContext.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName()?.Equals(rootTableName, System.StringComparison.OrdinalIgnoreCase) ?? false)
            ?? throw new System.ArgumentException($"Table '{rootTableName}' not found in the DbContext model.");

        var dataMap = new DataMap { Tables = new List<TableMap>() };
        var template = BuildNode(rootEntityType, dataMap, new HashSet<string>(), null);

        return (template, dataMap);
    }

    /// <summary>
    /// Recursively builds a JObject node for an entity, including its parent and child relationships.
    /// </summary>
    private static JObject BuildNode(IEntityType entityType, DataMap dataMap, ISet<string> visitedNavigations, IEntityType? parentEntityType)
    {
        var node = new JObject();
        var tableName = entityType.GetTableName()!;
        var tableMap = new TableMap { Name = tableName, Lookups = new List<LookupRule>() };

        // 1. Add scalar properties with annotated placeholders.
        foreach (var property in entityType.GetProperties().Where(p => !p.IsForeignKey() && !p.IsPrimaryKey()))
        {
            node[property.Name] = GeneratePlaceholder(property);
        }

        // 2. Add PARENT/LOOKUP relationships.
        foreach (var foreignKey in entityType.GetForeignKeys())
        {
            var principalEntityType = foreignKey.PrincipalEntityType;

            // Skip this foreign key if it points back to the parent that initiated this recursive call.
            if (principalEntityType == parentEntityType)
            {
                continue;
            }

            // The "friendly name" for a lookup is the name of the entity it points to.
            var dataPropertyName = principalEntityType.ClrType.Name;
            var lookupColumn = FindBestLookupColumn(principalEntityType);

            node[dataPropertyName] = $"TODO: Add lookup value for {dataPropertyName} (e.g., a {lookupColumn})";

            tableMap.Lookups.Add(new LookupRule
            {
                DataProperty = dataPropertyName,
                LookupTable = principalEntityType.GetTableName()!,
                LookupValueColumn = lookupColumn
            });
        }

        // 3. Add CHILD relationships.
        foreach (var navigation in entityType.GetNavigations().Where(n => n.IsCollection))
        {
            if (!visitedNavigations.Add(navigation.Name)) continue;

            var childEntityType = navigation.TargetEntityType;
            var childNode = BuildNode(childEntityType, dataMap, visitedNavigations, null);
            node[navigation.Name] = new JArray(childNode);
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
            sb.Append($", default SQL: {defaultValueSql}");
        }
        return sb.ToString();
    }

    private static string FindBestLookupColumn(IEntityType entityType)
    {
        var pkName = entityType.FindPrimaryKey()!.Properties.First().Name;

        // Rule 1: Prioritize common "friendly" names that are strings.
        var priorityNames = new[] { "Name", "Title", "Description", "Key", "Code", "Email" };
        foreach (var name in priorityNames)
        {
            var property = entityType.GetProperties()
                .FirstOrDefault(p => p.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase) && p.ClrType == typeof(string));

            if (property != null)
            {
                return property.GetColumnName()!;
            }
        }

        // Rule 2: If no priority names are found, find the FIRST string property
        // that is NOT the primary key.
        var firstStringProperty = entityType.GetProperties()
            .FirstOrDefault(p => p.ClrType == typeof(string) && !p.Name.Equals(pkName, System.StringComparison.OrdinalIgnoreCase));

        if (firstStringProperty != null)
        {
            return firstStringProperty.GetColumnName()!;
        }

        // Rule 3: As a last resort, fall back to the primary key.
        return entityType.FindPrimaryKey()!.Properties.First().GetColumnName()!;
    }
}