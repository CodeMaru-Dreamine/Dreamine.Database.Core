using System.Reflection;
using System.Collections.Concurrent;
using Dreamine.Database.Abstractions.Mapping;

namespace Dreamine.Database.Core.Mapping;

/// <summary>
/// Describes database mapping metadata for an entity type.
/// </summary>
public sealed class DatabaseEntityMap
{
    private static readonly ConcurrentDictionary<Type, DatabaseEntityMap> Cache = new();

    private DatabaseEntityMap(Type entityType, string tableName, IReadOnlyList<DatabasePropertyMap> properties)
    {
        EntityType = entityType;
        TableName = tableName;
        Properties = properties;
        Key = properties.FirstOrDefault(x => x.IsKey);
        InsertableProperties = properties.Where(x => !x.IsGenerated).ToArray();
        UpdatableProperties = properties.Where(x => !x.IsGenerated && !x.IsKey).ToArray();
    }

    /// <summary>
    /// Gets the mapped entity type.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Gets the mapped table name.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Gets all mapped properties.
    /// </summary>
    public IReadOnlyList<DatabasePropertyMap> Properties { get; }

    /// <summary>
    /// Gets the mapped key property, if one exists.
    /// </summary>
    public DatabasePropertyMap? Key { get; }

    /// <summary>
    /// Gets properties that can be included in insert statements.
    /// </summary>
    public IReadOnlyList<DatabasePropertyMap> InsertableProperties { get; }

    /// <summary>
    /// Gets properties that can be included in update statements.
    /// </summary>
    public IReadOnlyList<DatabasePropertyMap> UpdatableProperties { get; }

    /// <summary>
    /// Creates or retrieves mapping metadata for an entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The cached entity map.</returns>
    public static DatabaseEntityMap Create<T>()
    {
        return Create(typeof(T));
    }

    /// <summary>
    /// Creates or retrieves mapping metadata for an entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The cached entity map.</returns>
    public static DatabaseEntityMap Create(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return Cache.GetOrAdd(entityType, CreateUncached);
    }

    private static DatabaseEntityMap CreateUncached(Type entityType)
    {
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
