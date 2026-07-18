using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Dreamine.Database.Abstractions;
using Dreamine.Database.Core.Mapping;

namespace Dreamine.Database.Core.Providers;

/// <summary>
/// \if KO
/// <para>Dreamine 데이터베이스 공급자의 공통 기본 구현을 제공합니다.</para>
/// \endif
/// \if EN
/// <para>Provides a common base implementation for Dreamine database providers.</para>
/// \endif
/// </summary>
public abstract class DatabaseProviderBase : IDatabaseProvider
{
    /// <summary>
    /// \if KO
    /// <para>캐시할 데이터 조작 SQL의 종류를 나타냅니다.</para>
    /// \endif
    /// \if EN
    /// <para>Identifies the kind of data-manipulation SQL stored in the cache.</para>
    /// \endif
    /// </summary>
    private enum SqlKind
    {
        /// <summary>
        /// \if KO
        /// <para>삽입 SQL입니다.</para>
        /// \endif
        /// \if EN
        /// <para>Insert SQL.</para>
        /// \endif
        /// </summary>
        Insert,
        /// <summary>
        /// \if KO
        /// <para>갱신 SQL입니다.</para>
        /// \endif
        /// \if EN
        /// <para>Update SQL.</para>
        /// \endif
        /// </summary>
        Update,
        /// <summary>
        /// \if KO
        /// <para>삭제 SQL입니다.</para>
        /// \endif
        /// \if EN
        /// <para>Delete SQL.</para>
        /// \endif
        /// </summary>
        Delete
    }

    // Keyed by (entity type, provider kind, sql kind) — avoids re-building identical SQL on every call.
    /// <summary>
    /// \if KO
    /// <para>Sql Cache 값을 보관합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Stores the sql cache value.</para>
    /// \endif
    /// </summary>
    private static readonly ConcurrentDictionary<(Type, DatabaseProviderKind, SqlKind), string> SqlCache = new();

    /// <summary>
    /// \if KO
    /// <para>지정한 연결 문자열로 <see cref="DatabaseProviderBase"/>의 새 인스턴스를 초기화합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Initializes a new <see cref="DatabaseProviderBase"/> instance with the specified connection string.</para>
    /// \endif
    /// </summary>
    /// <param name="connectionString">
    /// \if KO
    /// <para>공급자가 사용할 연결 문자열입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The connection string used by the provider.</para>
    /// \endif
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="connectionString"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="connectionString"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="connectionString"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="connectionString"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    protected DatabaseProviderBase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    /// <summary>
    /// \if KO
    /// <para>구현 공급자의 종류를 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the concrete provider kind.</para>
    /// \endif
    /// </summary>
    public abstract DatabaseProviderKind Kind { get; }

    /// <summary>
    /// \if KO
    /// <para>데이터베이스 연결 문자열을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the database connection string.</para>
    /// \endif
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// \if KO
    /// <para>SQL 매개 변수 이름 앞에 붙는 공급자별 접두사를 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the provider-specific prefix placed before SQL parameter names.</para>
    /// \endif
    /// </summary>
    protected virtual string ParameterPrefix => "@";

    /// <summary>
    /// \if KO
    /// <para>대상 데이터베이스를 열어 존재 여부를 동기적으로 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Synchronously verifies the target database by opening it.</para>
    /// \endif
    /// </summary>
    public virtual void EnsureDatabaseExists()
    {
        using var connection = CreateConnection();
        connection.Open();
    }

    /// <summary>
    /// \if KO
    /// <para>대상 데이터베이스를 열어 존재 여부를 비동기적으로 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously verifies the target database by opening it.</para>
    /// \endif
    /// </summary>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>열기 작업 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the open operation.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>데이터베이스 확인 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task representing the database verification.</para>
    /// \endif
    /// </returns>
    public virtual async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// \if KO
    /// <para><typeparamref name="T"/>에 매핑된 테이블이 존재하는지 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Determines whether the table mapped to <typeparamref name="T"/> exists.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>확인할 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type to inspect.</para>
    /// \endif
    /// </typeparam>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부입니다.</para>
    /// \endif
    /// \if EN
    /// <para>Whether the table exists.</para>
    /// \endif
    /// </returns>
    public bool IsTableExists<T>()
    {
        return IsTableExists(DatabaseEntityMap.Create<T>().TableName);
    }

    /// <summary>
    /// \if KO
    /// <para><typeparamref name="T"/>에 매핑된 테이블이 존재하는지 비동기적으로 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously determines whether the table mapped to <typeparamref name="T"/> exists.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>확인할 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type to inspect.</para>
    /// \endif
    /// </typeparam>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>확인 작업 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the inspection.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result indicates whether the table exists.</para>
    /// \endif
    /// </returns>
    public Task<bool> IsTableExistsAsync<T>(CancellationToken cancellationToken = default)
    {
        return IsTableExistsAsync(DatabaseEntityMap.Create<T>().TableName, cancellationToken);
    }

    /// <summary>
    /// \if KO
    /// <para>지정한 이름의 테이블이 존재하는지 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Determines whether a table with the specified name exists.</para>
    /// \endif
    /// </summary>
    /// <param name="tableName">
    /// \if KO
    /// <para>확인할 테이블 이름입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table name to inspect.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부입니다.</para>
    /// \endif
    /// \if EN
    /// <para>Whether the table exists.</para>
    /// \endif
    /// </returns>
    public abstract bool IsTableExists(string tableName);

    /// <summary>
    /// \if KO
    /// <para>지정한 이름의 테이블이 존재하는지 비동기적으로 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously determines whether a table with the specified name exists.</para>
    /// \endif
    /// </summary>
    /// <param name="tableName">
    /// \if KO
    /// <para>확인할 테이블 이름입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table name to inspect.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>확인 작업 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the inspection.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result indicates whether the table exists.</para>
    /// \endif
    /// </returns>
    public abstract Task<bool> IsTableExistsAsync(
        string tableName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// \if KO
    /// <para><typeparamref name="T"/>에 매핑된 테이블이 없으면 생성합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates the table mapped to <typeparamref name="T"/> when it does not exist.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>테이블을 정의하는 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type that defines the table.</para>
    /// \endif
    /// </typeparam>
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

    /// <summary>
    /// \if KO
    /// <para><typeparamref name="T"/>에 매핑된 테이블이 없으면 비동기적으로 생성합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously creates the table mapped to <typeparamref name="T"/> when it does not exist.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>테이블을 정의하는 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type that defines the table.</para>
    /// \endif
    /// </typeparam>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>생성 작업 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel table creation.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 확인 및 생성 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task representing table verification and creation.</para>
    /// \endif
    /// </returns>
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

    /// <summary>
    /// \if KO
    /// <para>결과 집합을 반환하지 않는 SQL 명령을 동기적으로 실행합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Synchronously executes a SQL command that does not return a result set.</para>
    /// \endif
    /// </summary>
    /// <param name="sql">
    /// \if KO
    /// <para>실행할 SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL to execute.</para>
    /// \endif
    /// </param>
    /// <param name="parameters">
    /// \if KO
    /// <para>선택적 SQL 매개 변수 객체입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The optional SQL-parameter object.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>영향을 받은 행 수입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The number of affected rows.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="sql"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="sql"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    public int ExecuteNonQuery(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateOpenedConnection();
        return connection.Execute(sql, parameters);
    }

    /// <summary>
    /// \if KO
    /// <para>결과 집합을 반환하지 않는 SQL 명령을 비동기적으로 실행합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously executes a SQL command that does not return a result set.</para>
    /// \endif
    /// </summary>
    /// <param name="sql">
    /// \if KO
    /// <para>실행할 SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL to execute.</para>
    /// \endif
    /// </param>
    /// <param name="parameters">
    /// \if KO
    /// <para>선택적 SQL 매개 변수 객체입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The optional SQL-parameter object.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>실행 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel execution.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>영향을 받은 행 수를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result is the number of affected rows.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="sql"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="sql"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is empty or white space.</para>
    /// \endif
    /// </exception>
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

    /// <summary>
    /// \if KO
    /// <para>SQL 명령을 실행하고 첫 번째 스칼라 값을 반환합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Executes a SQL command and returns its first scalar value.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>결과 값 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The result value type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="sql">
    /// \if KO
    /// <para>실행할 SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL to execute.</para>
    /// \endif
    /// </param>
    /// <param name="parameters">
    /// \if KO
    /// <para>선택적 SQL 매개 변수 객체입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The optional SQL-parameter object.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>변환된 스칼라 값입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The converted scalar value.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="sql"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="sql"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    public T? ExecuteScalar<T>(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateOpenedConnection();
        return connection.ExecuteScalar<T>(sql, parameters);
    }

    /// <summary>
    /// \if KO
    /// <para>SQL 명령을 비동기적으로 실행하고 첫 번째 스칼라 값을 반환합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously executes a SQL command and returns its first scalar value.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>결과 값 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The result value type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="sql">
    /// \if KO
    /// <para>실행할 SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL to execute.</para>
    /// \endif
    /// </param>
    /// <param name="parameters">
    /// \if KO
    /// <para>선택적 SQL 매개 변수 객체입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The optional SQL-parameter object.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>실행 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel execution.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>변환된 스칼라 값을 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result is the converted scalar value.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="sql"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="sql"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is empty or white space.</para>
    /// \endif
    /// </exception>
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

    /// <summary>
    /// \if KO
    /// <para>SQL 조회를 실행하고 행을 <typeparamref name="T"/>로 매핑합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Executes a SQL query and maps its rows to <typeparamref name="T"/>.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>행 매핑 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The row-mapping type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="sql">
    /// \if KO
    /// <para>실행할 SQL 조회문입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL query to execute.</para>
    /// \endif
    /// </param>
    /// <param name="parameters">
    /// \if KO
    /// <para>선택적 조회 매개 변수 객체입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The optional query-parameter object.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>메모리에 구체화된 매핑 행 시퀀스입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A materialized sequence of mapped rows.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="sql"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="sql"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    public IEnumerable<T> Query<T>(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var connection = CreateOpenedConnection();
        return connection.Query<T>(sql, parameters).ToArray();
    }

    /// <summary>
    /// \if KO
    /// <para>SQL 조회를 비동기적으로 실행하고 행을 <typeparamref name="T"/>로 매핑합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously executes a SQL query and maps its rows to <typeparamref name="T"/>.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>행 매핑 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The row-mapping type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="sql">
    /// \if KO
    /// <para>실행할 SQL 조회문입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL query to execute.</para>
    /// \endif
    /// </param>
    /// <param name="parameters">
    /// \if KO
    /// <para>선택적 조회 매개 변수 객체입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The optional query-parameter object.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>조회 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the query.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>매핑된 행의 읽기 전용 목록을 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result is a read-only list of mapped rows.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="sql"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="sql"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="sql"/> is empty or white space.</para>
    /// \endif
    /// </exception>
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

    /// <summary>
    /// \if KO
    /// <para>엔터티를 삽입합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Inserts an entity.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="entity">
    /// \if KO
    /// <para>삽입할 엔터티입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity to insert.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>행이 삽입되었는지 여부입니다.</para>
    /// \endif
    /// \if EN
    /// <para>Whether a row was inserted.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="entity"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="entity"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    public bool Insert<T>(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateOpenedConnection();
        return connection.Execute(GetOrBuildSql<T>(SqlKind.Insert), entity) > 0;
    }

    /// <summary>
    /// \if KO
    /// <para>엔터티를 비동기적으로 삽입합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously inserts an entity.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="entity">
    /// \if KO
    /// <para>삽입할 엔터티입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity to insert.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>삽입 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel insertion.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>행 삽입 여부를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result indicates whether a row was inserted.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="entity"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="entity"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    public async Task<bool> InsertAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(
                new CommandDefinition(GetOrBuildSql<T>(SqlKind.Insert), entity, cancellationToken: cancellationToken))
            .ConfigureAwait(false) > 0;
    }

    /// <summary>
    /// \if KO
    /// <para>키로 식별된 엔터티 행을 갱신합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Updates the entity row identified by its key.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="entity">
    /// \if KO
    /// <para>갱신할 엔터티입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity to update.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>행이 갱신되었는지 여부입니다.</para>
    /// \endif
    /// \if EN
    /// <para>Whether a row was updated.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="entity"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="entity"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// \if KO
    /// <para>엔터티 매핑에 키가 없는 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when the entity mapping does not define a key.</para>
    /// \endif
    /// </exception>
    public bool Update<T>(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateOpenedConnection();
        return connection.Execute(GetOrBuildSql<T>(SqlKind.Update), entity) > 0;
    }

    /// <summary>
    /// \if KO
    /// <para>키로 식별된 엔터티 행을 비동기적으로 갱신합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously updates the entity row identified by its key.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="entity">
    /// \if KO
    /// <para>갱신할 엔터티입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity to update.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>갱신 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the update.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>행 갱신 여부를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result indicates whether a row was updated.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="entity"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="entity"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// \if KO
    /// <para>엔터티 매핑에 키가 없는 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when the entity mapping does not define a key.</para>
    /// \endif
    /// </exception>
    public async Task<bool> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(
                new CommandDefinition(GetOrBuildSql<T>(SqlKind.Update), entity, cancellationToken: cancellationToken))
            .ConfigureAwait(false) > 0;
    }

    /// <summary>
    /// \if KO
    /// <para>키로 식별된 엔터티 행을 삭제합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Deletes the entity row identified by its key.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="entity">
    /// \if KO
    /// <para>삭제할 엔터티입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity to delete.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>행이 삭제되었는지 여부입니다.</para>
    /// \endif
    /// \if EN
    /// <para>Whether a row was deleted.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="entity"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="entity"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// \if KO
    /// <para>엔터티 매핑에 키가 없는 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when the entity mapping does not define a key.</para>
    /// \endif
    /// </exception>
    public bool Delete<T>(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateOpenedConnection();
        return connection.Execute(GetOrBuildSql<T>(SqlKind.Delete), entity) > 0;
    }

    /// <summary>
    /// \if KO
    /// <para>키로 식별된 엔터티 행을 비동기적으로 삭제합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously deletes the entity row identified by its key.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="entity">
    /// \if KO
    /// <para>삭제할 엔터티입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity to delete.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>삭제 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel deletion.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>행 삭제 여부를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result indicates whether a row was deleted.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="entity"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="entity"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// \if KO
    /// <para>엔터티 매핑에 키가 없는 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when the entity mapping does not define a key.</para>
    /// \endif
    /// </exception>
    public async Task<bool> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var connection = CreateConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(
                new CommandDefinition(GetOrBuildSql<T>(SqlKind.Delete), entity, cancellationToken: cancellationToken))
            .ConfigureAwait(false) > 0;
    }

    /// <summary>
    /// \if KO
    /// <para>공급자별 닫힌 데이터베이스 연결을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates a provider-specific closed database connection.</para>
    /// \endif
    /// </summary>
    /// <returns>
    /// \if KO
    /// <para>새 데이터베이스 연결입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A new database connection.</para>
    /// \endif
    /// </returns>
    protected abstract IDbConnection CreateConnection();

    /// <summary>
    /// \if KO
    /// <para>공급자 문법에 맞게 SQL 식별자를 인용합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Quotes a SQL identifier using provider syntax.</para>
    /// \endif
    /// </summary>
    /// <param name="identifier">
    /// \if KO
    /// <para>인용할 식별자입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The identifier to quote.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>인용된 식별자입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The quoted identifier.</para>
    /// \endif
    /// </returns>
    protected abstract string QuoteIdentifier(string identifier);

    /// <summary>
    /// \if KO
    /// <para>속성 매핑에 대응하는 공급자별 SQL 열 형식을 반환합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Returns the provider-specific SQL column type for a property mapping.</para>
    /// \endif
    /// </summary>
    /// <param name="property">
    /// \if KO
    /// <para>변환할 속성 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The property mapping to convert.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>SQL 열 형식 선언입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL column-type declaration.</para>
    /// \endif
    /// </returns>
    protected abstract string GetSqlType(DatabasePropertyMap property);

    /// <summary>
    /// \if KO
    /// <para>엔터티 매핑의 CREATE TABLE SQL을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Builds CREATE TABLE SQL for an entity map.</para>
    /// \endif
    /// </summary>
    /// <param name="map">
    /// \if KO
    /// <para>테이블을 정의하는 엔터티 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity map defining the table.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>CREATE TABLE SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The CREATE TABLE SQL.</para>
    /// \endif
    /// </returns>
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

    /// <summary>
    /// \if KO
    /// <para>엔터티 매핑의 INSERT SQL을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Builds INSERT SQL for an entity map.</para>
    /// \endif
    /// </summary>
    /// <param name="map">
    /// \if KO
    /// <para>삽입 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The insertion map.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>INSERT SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The INSERT SQL.</para>
    /// \endif
    /// </returns>
    protected virtual string BuildInsertSql(DatabaseEntityMap map)
    {
        var properties = map.InsertableProperties;
        var columns = string.Join(", ", properties.Select(x => QuoteIdentifier(x.ColumnName)));
        var values = string.Join(", ", properties.Select(x => ParameterPrefix + x.Property.Name));
        return $"INSERT INTO {QuoteIdentifier(map.TableName)} ({columns}) VALUES ({values})";
    }

    /// <summary>
    /// \if KO
    /// <para>엔터티 매핑의 UPDATE SQL을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Builds UPDATE SQL for an entity map.</para>
    /// \endif
    /// </summary>
    /// <param name="map">
    /// \if KO
    /// <para>갱신 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The update map.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>UPDATE SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The UPDATE SQL.</para>
    /// \endif
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// \if KO
    /// <para>매핑에 키가 없는 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when the mapping has no key.</para>
    /// \endif
    /// </exception>
    protected virtual string BuildUpdateSql(DatabaseEntityMap map)
    {
        var key = RequireKey(map);
        var assignments = string.Join(
            ", ",
            map.UpdatableProperties.Select(x => $"{QuoteIdentifier(x.ColumnName)} = {ParameterPrefix}{x.Property.Name}"));
        return $"UPDATE {QuoteIdentifier(map.TableName)} SET {assignments} WHERE {QuoteIdentifier(key.ColumnName)} = {ParameterPrefix}{key.Property.Name}";
    }

    /// <summary>
    /// \if KO
    /// <para>엔터티 매핑의 DELETE SQL을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Builds DELETE SQL for an entity map.</para>
    /// \endif
    /// </summary>
    /// <param name="map">
    /// \if KO
    /// <para>삭제 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The deletion map.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>DELETE SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The DELETE SQL.</para>
    /// \endif
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// \if KO
    /// <para>매핑에 키가 없는 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when the mapping has no key.</para>
    /// \endif
    /// </exception>
    protected virtual string BuildDeleteSql(DatabaseEntityMap map)
    {
        var key = RequireKey(map);
        return $"DELETE FROM {QuoteIdentifier(map.TableName)} WHERE {QuoteIdentifier(key.ColumnName)} = {ParameterPrefix}{key.Property.Name}";
    }

    /// <summary>
    /// \if KO
    /// <para>기본 키 열에 덧붙일 공급자별 SQL 조각을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Builds the provider-specific SQL fragment appended to a primary-key column.</para>
    /// \endif
    /// </summary>
    /// <param name="property">
    /// \if KO
    /// <para>기본 키 속성 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The primary-key property mapping.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>기본 키 SQL 조각입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The primary-key SQL fragment.</para>
    /// \endif
    /// </returns>
    protected virtual string BuildPrimaryKeySql(DatabasePropertyMap property)
    {
        return " PRIMARY KEY";
    }

    /// <summary>
    /// \if KO
    /// <para>데이터 조작 SQL을 캐시에서 가져오거나 생성합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Retrieves data-manipulation SQL from the cache or builds it.</para>
    /// \endif
    /// </summary>
    /// <typeparam name="T">
    /// \if KO
    /// <para>대상 엔터티 형식입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The target entity type.</para>
    /// \endif
    /// </typeparam>
    /// <param name="kind">
    /// \if KO
    /// <para>필요한 SQL 작업 종류입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The required SQL operation kind.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>캐시되었거나 새로 생성된 SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The cached or newly built SQL.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// \if KO
    /// <para>지원하지 않는 <paramref name="kind"/> 값인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="kind"/> is unsupported.</para>
    /// \endif
    /// </exception>
    private string GetOrBuildSql<T>(SqlKind kind)
    {
        var key = (typeof(T), Kind, kind);
        return SqlCache.GetOrAdd(key, _ =>
        {
            var map = DatabaseEntityMap.Create<T>();
            return kind switch
            {
                SqlKind.Insert => BuildInsertSql(map),
                SqlKind.Update => BuildUpdateSql(map),
                SqlKind.Delete => BuildDeleteSql(map),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        });
    }

    /// <summary>
    /// \if KO
    /// <para>새 공급자 연결을 만들고 동기적으로 엽니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates and synchronously opens a new provider connection.</para>
    /// \endif
    /// </summary>
    /// <returns>
    /// \if KO
    /// <para>열린 데이터베이스 연결입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The opened database connection.</para>
    /// \endif
    /// </returns>
    private IDbConnection CreateOpenedConnection()
    {
        var connection = CreateConnection();
        connection.Open();
        return connection;
    }

    /// <summary>
    /// \if KO
    /// <para>가능한 경우 비동기 API로 데이터베이스 연결을 엽니다.</para>
    /// \endif
    /// \if EN
    /// <para>Opens a database connection using an asynchronous API when available.</para>
    /// \endif
    /// </summary>
    /// <param name="connection">
    /// \if KO
    /// <para>열 데이터베이스 연결입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The database connection to open.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>열기 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel opening.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>연결 열기 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task representing connection opening.</para>
    /// \endif
    /// </returns>
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

    /// <summary>
    /// \if KO
    /// <para>엔터티 매핑에서 필수 키 속성을 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the required key property from an entity map.</para>
    /// \endif
    /// </summary>
    /// <param name="map">
    /// \if KO
    /// <para>검사할 엔터티 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The entity map to inspect.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>매핑된 키 속성입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The mapped key property.</para>
    /// \endif
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// \if KO
    /// <para>키 속성이 정의되지 않은 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when no key property is defined.</para>
    /// \endif
    /// </exception>
    private static DatabasePropertyMap RequireKey(DatabaseEntityMap map)
    {
        return map.Key ?? throw new InvalidOperationException(
            $"Entity [{map.EntityType.FullName}] does not define a key. Add [DatabaseKey] or an Id property.");
    }
}
