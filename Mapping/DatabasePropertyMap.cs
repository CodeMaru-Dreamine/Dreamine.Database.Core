using System.Reflection;
using Dreamine.Database.Abstractions.Mapping;

namespace Dreamine.Database.Core.Mapping;

public sealed class DatabasePropertyMap
{
    private DatabasePropertyMap(PropertyInfo property, string columnName, bool isKey, bool isGenerated)
    {
        Property = property;
        ColumnName = columnName;
        IsKey = isKey;
        IsGenerated = isGenerated;
    }

    public PropertyInfo Property { get; }

    public string ColumnName { get; }

    public Type PropertyType => Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;

    public bool IsKey { get; }

    public bool IsGenerated { get; }

    public static DatabasePropertyMap Create(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var columnName = property.GetCustomAttribute<DatabaseColumnAttribute>()?.Name ?? property.Name;
        var isKey = property.GetCustomAttribute<DatabaseKeyAttribute>() is not null ||
                    string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase);
        var isGenerated = property.GetCustomAttribute<DatabaseGeneratedAttribute>() is not null;

        return new DatabasePropertyMap(property, columnName, isKey, isGenerated);
    }
}
