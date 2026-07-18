using System.Reflection;
using System.Collections.Concurrent;
using Dreamine.Database.Abstractions.Mapping;

namespace Dreamine.Database.Core.Mapping;

/// <summary>
/// \if KO
/// <para>엔터티 형식의 데이터베이스 매핑 메타데이터를 설명합니다.</para>
/// \endif
/// \if EN
/// <para>Describes database-mapping metadata for an entity type.</para>
/// \endif
/// </summary>
public sealed class DatabaseEntityMap
{
    /// <summary>
    /// \if KO
    /// <para>Cache 값을 보관합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Stores the cache value.</para>
    /// \endif
    /// </summary>
    private static readonly ConcurrentDictionary<Type, DatabaseEntityMap> Cache = new();

    /// <summary>
    /// \if KO
    /// <para>분석된 엔터티 형식과 테이블 및 속성 매핑으로 인스턴스를 초기화합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Initializes an instance with the analyzed entity type, table, and property mappings.</para>
    /// \endif
    /// </summary>
    /// <param name="entityType">
    /// \if KO
    /// <para>매핑된 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The mapped entity type.</para>
    /// \endif
    /// </param>
    /// <param name="tableName">
    /// \if KO
    /// <para>매핑된 테이블 이름입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The mapped table name.</para>
    /// \endif
    /// </param>
    /// <param name="properties">
    /// \if KO
    /// <para>엔터티의 속성 매핑 목록입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity property mappings.</para>
    /// \endif
    /// </param>
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
    /// \if KO
    /// <para>매핑된 엔터티 형식을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the mapped entity type.</para>
    /// \endif
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// \if KO
    /// <para>매핑된 테이블 이름을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the mapped table name.</para>
    /// \endif
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// \if KO
    /// <para>모든 매핑된 속성을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets all mapped properties.</para>
    /// \endif
    /// </summary>
    public IReadOnlyList<DatabasePropertyMap> Properties { get; }

    /// <summary>
    /// \if KO
    /// <para>존재하는 경우 매핑된 키 속성을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the mapped key property, when one exists.</para>
    /// \endif
    /// </summary>
    public DatabasePropertyMap? Key { get; }

    /// <summary>
    /// \if KO
    /// <para>INSERT 문에 포함할 수 있는 속성을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the properties that can be included in INSERT statements.</para>
    /// \endif
    /// </summary>
    public IReadOnlyList<DatabasePropertyMap> InsertableProperties { get; }

    /// <summary>
    /// \if KO
    /// <para>UPDATE 문에 포함할 수 있는 속성을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the properties that can be included in UPDATE statements.</para>
    /// \endif
    /// </summary>
    public IReadOnlyList<DatabasePropertyMap> UpdatableProperties { get; }

    /// <summary>
    /// \if KO
    /// <para>제네릭 엔터티 형식의 매핑 메타데이터를 생성하거나 캐시에서 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates or retrieves cached mapping metadata for a generic entity type.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>매핑할 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type to map.</para>
    /// \endif
    /// </typeparam>
    /// <returns>
    /// \if KO
    /// <para>생성되었거나 캐시된 엔터티 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The created or cached entity map.</para>
    /// \endif
    /// </returns>
    public static DatabaseEntityMap Create<T>()
    {
        return Create(typeof(T));
    }

    /// <summary>
    /// \if KO
    /// <para>지정한 엔터티 형식의 매핑 메타데이터를 생성하거나 캐시에서 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates or retrieves cached mapping metadata for the specified entity type.</para>
    /// \endif
    /// </summary>
    /// <param name="entityType">
    /// \if KO
    /// <para>매핑할 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type to map.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>생성되었거나 캐시된 엔터티 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The created or cached entity map.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="entityType"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="entityType"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    public static DatabaseEntityMap Create(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return Cache.GetOrAdd(entityType, CreateUncached);
    }

    /// <summary>
    /// \if KO
    /// <para>리플렉션을 사용하여 캐시되지 않은 새 엔터티 매핑을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Uses reflection to create a new, uncached entity map.</para>
    /// \endif
    /// </summary>
    /// <param name="entityType">
    /// \if KO
    /// <para>분석할 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type to analyze.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>분석된 엔터티 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The analyzed entity map.</para>
    /// \endif
    /// </returns>
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
