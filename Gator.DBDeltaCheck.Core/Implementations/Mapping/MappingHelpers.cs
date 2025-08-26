using Newtonsoft.Json.Linq;

namespace Gator.DBDeltaCheck.Core.Implementations.Mapping;

/// <summary>
/// Provides shared helper methods for data mapping operations.
/// </summary>
public static class MappingHelpers
{
    /// <summary>
    /// Converts a string override value to the target property's type and returns it as a JToken.
    /// </summary>
    public static JToken ApplyOverrideValue(string overrideValue, Type? propertyType)
    {
        if (propertyType == null)
        {
            return new JValue(overrideValue); // No type info, so just use the string.
        }

        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        object convertedValue;

        try
        {
            bool isNullable = propertyType.IsClass || Nullable.GetUnderlyingType(propertyType) != null;
            convertedValue = (string.IsNullOrEmpty(overrideValue) && isNullable)
                ? null
                : Convert.ChangeType(overrideValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            convertedValue = overrideValue; // If conversion fails, fall back to using the raw string.
        }
        return convertedValue == null ? JValue.CreateNull() : JToken.FromObject(convertedValue);
    }
}
