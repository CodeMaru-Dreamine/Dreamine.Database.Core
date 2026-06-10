using System.Reflection;
using Dreamine.Database.Abstractions.Mapping;

namespace Dreamine.Database.Core.Mapping;

public sealed class DatabaseEntityMap
{
    private DatabaseEntityMap(Type entityType, string tableName, IReadOnlyList<DatabasePropertyMap> properties)
    {
        EntityType = entityType;
        TableName = tableName;
        Properties = properties;
        Key = properties.FirstOrDefault(x => x.IsKey);
    }

    public Type EntityType { get; }

    public string TableName { get; }

    public IReadOnlyList<DatabasePropertyMap> Properties { get; }

    public DatabasePropertyMap? Key { get; }

    public IReadOnlyList<DatabasePropertyMap> InsertableProperties =>
        Properties.Where(x => !x.IsGenerated).ToArray();

    public IReadOnlyList<DatabasePropertyMap> UpdatableProperties =>
        Properties.Where(x => !x.IsGenerated && !x.IsKey).ToArray();

    public static DatabaseEntityMap Create<T>()
    {
        return Create(typeof(T));
    }

    public static DatabaseEntityMap Create(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var tableName = entityType.GetCustomAttribute<DatabaseTableAttribute>()?.Name ?? entityType.Name;
        var properties = entityType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.CanRead)
            .Where(x => x.GetCustomAttribute<DatabaseIgnoreAttribute>() is null)
            .Select(DatabasePropertyMap.Create)
            .ToArray();

        return new DatabaseEntityMap(entityType, tableName, properties);
    }
}
