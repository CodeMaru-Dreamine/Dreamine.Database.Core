using System.Data;
using Dapper;
using Dreamine.Database.Abstractions;
using Dreamine.Database.Core.Mapping;

namespace Dreamine.Database.Core.Providers;

/// <summary>
/// Provides a base implementation for Dreamine database providers.
/// </summary>
public abstract class DatabaseProviderBase : IDatabaseProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseProviderBase"/> class.
    /// </summary>
    /// <param name="connectionString">The provider connection string.</param>
    protected DatabaseProviderBase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    public abstract DatabaseProviderKind Kind { get; }

    public string ConnectionString { get; }

    protected virtual string ParameterPrefix => "@";

    public virtual void EnsureDatabaseExists()
    {
        using var connection = CreateConnection();
        connection.Open();
    }

    public virtual async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public bool IsTableExists<T>()
    {
        return IsTableExists(DatabaseEntityMap.Create<T>().TableName);
    }

    public Task<bool> IsTableExistsAsync<T>(CancellationToken cancellationToken = default)
    {
        return IsTableExistsAsync(DatabaseEntityMap.Create<T>().TableName, cancellationToken);
    }

    public abstract bool IsTableExists(string tableName);

    public abstract Task<bool> IsTableExistsAsync(
        string tableName,
        CancellationToken cancellationToken = default);

    public void CreateTable<T>()
    {
        var map = DatabaseEntityMap.Create<T>();
        if (IsTableExists(map.TableName))
        {
            return;
        }

        var sql = BuildCreateTableSql(map);
        ExecuteNonQuery(sql);
    }

    public async Task CreateTableAsync<T>(CancellationToken cancellationToken = default)
    {
        var map = DatabaseEntityMap.Create<T>();
        if (await IsTableExistsAsync(map.TableName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var sql = BuildCreateTableSql(map);
        await ExecuteNonQueryAsync(sql, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public int ExecuteNonQuery(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateOpenedConnection();
        return connection.Execute(sql, parameters);
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public T? ExecuteScalar<T>(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateOpenedConnection();
        return connection.ExecuteScalar<T>(sql, parameters);
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<T>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public IEnumerable<T> Query<T>(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateOpenedConnection();
        return connection.Query<T>(sql, parameters).ToArray();
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<T>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToArray();
    }

    public bool Insert<T>(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateOpenedConnection();
        return connection.Execute(BuildInsertSql(DatabaseEntityMap.Create<T>()), entity) > 0;
    }

    public async Task<bool> InsertAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(
                new CommandDefinition(BuildInsertSql(DatabaseEntityMap.Create<T>()), entity, cancellationToken: cancellationToken))
            .ConfigureAwait(false) > 0;
    }

    public bool Update<T>(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = DatabaseEntityMap.Create<T>();
        using var connection = CreateOpenedConnection();
        return connection.Execute(BuildUpdateSql(map), entity) > 0;
    }

    public async Task<bool> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = DatabaseEntityMap.Create<T>();
        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(
                new CommandDefinition(BuildUpdateSql(map), entity, cancellationToken: cancellationToken))
            .ConfigureAwait(false) > 0;
    }

    public bool Delete<T>(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = DatabaseEntityMap.Create<T>();
        using var connection = CreateOpenedConnection();
        return connection.Execute(BuildDeleteSql(map), entity) > 0;
    }

    public async Task<bool> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = DatabaseEntityMap.Create<T>();
        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(
                new CommandDefinition(BuildDeleteSql(map), entity, cancellationToken: cancellationToken))
            .ConfigureAwait(false) > 0;
    }

    protected abstract IDbConnection CreateConnection();

    protected abstract string QuoteIdentifier(string identifier);

    protected abstract string GetSqlType(DatabasePropertyMap property);

    protected virtual string BuildCreateTableSql(DatabaseEntityMap map)
    {
        var columns = map.Properties.Select(property =>
        {
            var sql = $"{QuoteIdentifier(property.ColumnName)} {GetSqlType(property)}";
            if (property.IsKey)
            {
                sql += BuildPrimaryKeySql(property);
            }

            return sql;
        });

        return $"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(map.TableName)} ({string.Join(", ", columns)})";
    }

    protected virtual string BuildInsertSql(DatabaseEntityMap map)
    {
        var properties = map.InsertableProperties;
        var columns = string.Join(", ", properties.Select(x => QuoteIdentifier(x.ColumnName)));
        var values = string.Join(", ", properties.Select(x => ParameterPrefix + x.Property.Name));
        return $"INSERT INTO {QuoteIdentifier(map.TableName)} ({columns}) VALUES ({values})";
    }

    protected virtual string BuildUpdateSql(DatabaseEntityMap map)
    {
        var key = RequireKey(map);
        var assignments = string.Join(
            ", ",
            map.UpdatableProperties.Select(x => $"{QuoteIdentifier(x.ColumnName)} = {ParameterPrefix}{x.Property.Name}"));
        return $"UPDATE {QuoteIdentifier(map.TableName)} SET {assignments} WHERE {QuoteIdentifier(key.ColumnName)} = {ParameterPrefix}{key.Property.Name}";
    }

    protected virtual string BuildDeleteSql(DatabaseEntityMap map)
    {
        var key = RequireKey(map);
        return $"DELETE FROM {QuoteIdentifier(map.TableName)} WHERE {QuoteIdentifier(key.ColumnName)} = {ParameterPrefix}{key.Property.Name}";
    }

    protected virtual string BuildPrimaryKeySql(DatabasePropertyMap property)
    {
        return " PRIMARY KEY";
    }

    private IDbConnection CreateOpenedConnection()
    {
        var connection = CreateConnection();
        connection.Open();
        return connection;
    }

    private static async Task OpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is System.Data.Common.DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        connection.Open();
    }

    private static DatabasePropertyMap RequireKey(DatabaseEntityMap map)
    {
        return map.Key ?? throw new InvalidOperationException(
            $"Entity [{map.EntityType.FullName}] does not define a key. Add [DatabaseKey] or an Id property.");
    }
}
