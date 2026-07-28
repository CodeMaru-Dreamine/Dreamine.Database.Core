using System.Data;
using Dreamine.Database.Abstractions;
using Dreamine.Database.Abstractions.Mapping;
using Dreamine.Database.Core.Mapping;
using Dreamine.Database.Core.Providers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dreamine.Database.Core.Tests;

public sealed class MappingAndSqlTests
{
    [Fact]
    public void EntityMap_UsesAttributesAndFiltersIgnoredProperties()
    {
        var map = DatabaseEntityMap.Create<SampleEntity>();

        Assert.Equal("sample_records", map.TableName);
        Assert.Same(map, DatabaseEntityMap.Create<SampleEntity>());
        Assert.Equal(nameof(SampleEntity.Id), map.Key?.Property.Name);
        Assert.Equal("display_name", map.Properties.Single(x => x.Property.Name == nameof(SampleEntity.Name)).ColumnName);
        Assert.DoesNotContain(map.Properties, x => x.Property.Name == nameof(SampleEntity.Transient));
        Assert.DoesNotContain(map.InsertableProperties, x => x.Property.Name == nameof(SampleEntity.Id));
        Assert.DoesNotContain(map.UpdatableProperties, x => x.Property.Name == nameof(SampleEntity.Id));
    }

    [Fact]
    public void PropertyMap_UnwrapsNullableTypesAndRecognizesIdConvention()
    {
        var id = DatabasePropertyMap.Create(typeof(ConventionEntity).GetProperty(nameof(ConventionEntity.Id))!);
        var count = DatabasePropertyMap.Create(typeof(ConventionEntity).GetProperty(nameof(ConventionEntity.Count))!);

        Assert.True(id.IsKey);
        Assert.False(id.IsGenerated);
        Assert.Equal(typeof(int), count.PropertyType);
    }

    [Fact]
    public void ProviderBase_BuildsQuotedCrudStatements()
    {
        var provider = new TestProvider();
        var map = DatabaseEntityMap.Create<SampleEntity>();

        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS [sample_records] ([Id] INTEGER PRIMARY KEY, [display_name] TEXT, [Count] INTEGER)",
            provider.CreateTableSql(map));
        Assert.Equal(
            "INSERT INTO [sample_records] ([display_name], [Count]) VALUES (@Name, @Count)",
            provider.InsertSql(map));
        Assert.Equal(
            "UPDATE [sample_records] SET [display_name] = @Name, [Count] = @Count WHERE [Id] = @Id",
            provider.UpdateSql(map));
        Assert.Equal(
            "DELETE FROM [sample_records] WHERE [Id] = @Id",
            provider.DeleteSql(map));
    }

    [Fact]
    public void ProviderBase_RejectsUpdatesForEntitiesWithoutAKey()
    {
        var provider = new TestProvider();
        var map = DatabaseEntityMap.Create<NoKeyEntity>();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.UpdateSql(map));

        Assert.Contains("does not define a key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderBase_ExecutesSyncAndAsyncCrudFlows()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"dreamine-database-core-{Guid.NewGuid():N}.db");

        try
        {
            var provider = new SqliteTestProvider($"Data Source={databasePath};Pooling=False");
            provider.EnsureDatabaseExists();
            await provider.EnsureDatabaseExistsAsync();

            provider.CreateTable<SampleEntity>();
            provider.CreateTable<SampleEntity>();
            await provider.CreateTableAsync<SampleEntity>();

            Assert.True(provider.IsTableExists<SampleEntity>());
            Assert.True(await provider.IsTableExistsAsync<SampleEntity>());

            var first = new SampleEntity { Name = "Alpha", Count = 1 };
            Assert.True(provider.Insert(first));

            var firstId = provider.ExecuteScalar<long>(
                "SELECT Id FROM sample_records WHERE display_name = @Name",
                new { first.Name });
            Assert.True(firstId > 0);

            var loaded = provider.Query<SampleEntity>(
                "SELECT Id, display_name AS Name, Count FROM sample_records");
            Assert.Single(loaded);
            Assert.Equal("Alpha", loaded.Single().Name);

            first.Id = firstId;
            first.Name = "Updated";
            first.Count = 2;
            Assert.True(await provider.UpdateAsync(first));

            var asyncRows = await provider.QueryAsync<SampleEntity>(
                "SELECT Id, display_name AS Name, Count FROM sample_records");
            Assert.Equal("Updated", Assert.Single(asyncRows).Name);

            Assert.True(await provider.DeleteAsync(first));
            Assert.Equal(0L, await provider.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sample_records"));

            var second = new SampleEntity { Name = "Beta", Count = 3 };
            Assert.True(await provider.InsertAsync(second));
            second.Id = await provider.ExecuteScalarAsync<long>(
                "SELECT Id FROM sample_records WHERE display_name = @Name",
                new { second.Name });
            Assert.True(provider.Update(second));
            Assert.True(provider.Delete(second));

            Assert.Equal(1, provider.ExecuteNonQuery(
                "INSERT INTO sample_records (display_name, Count) VALUES (@Name, @Count)",
                new { Name = "Gamma", Count = 4 }));
            Assert.Equal(1, await provider.ExecuteNonQueryAsync(
                "DELETE FROM sample_records WHERE display_name = @Name",
                new { Name = "Gamma" }));
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public void PublicGuards_RejectInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => DatabaseEntityMap.Create(null!));
        Assert.Throws<ArgumentNullException>(() => DatabasePropertyMap.Create(null!));
        Assert.Throws<ArgumentException>(() => new SqliteTestProvider(" "));

        var provider = new SqliteTestProvider("Data Source=:memory:");
        Assert.Throws<ArgumentException>(() => provider.ExecuteNonQuery(""));
        Assert.Throws<ArgumentException>(() => provider.ExecuteScalar<int>(" "));
        Assert.Throws<ArgumentException>(() => provider.Query<int>(""));
    }

    [DatabaseTable("sample_records")]
    private sealed class SampleEntity
    {
        [DatabaseKey]
        [DatabaseGenerated]
        public long Id { get; set; }

        [DatabaseColumn("display_name")]
        public string Name { get; set; } = "";

        public int? Count { get; set; }

        [DatabaseIgnore]
        public string Transient { get; set; } = "";
    }

    private sealed class ConventionEntity
    {
        public Guid Id { get; set; }

        public int? Count { get; set; }
    }

    private sealed class NoKeyEntity
    {
        public string Value { get; set; } = "";
    }

    private sealed class TestProvider : DatabaseProviderBase
    {
        public TestProvider()
            : base("test")
        {
        }

        public override DatabaseProviderKind Kind => DatabaseProviderKind.Unknown;

        public string CreateTableSql(DatabaseEntityMap map) => BuildCreateTableSql(map);

        public string InsertSql(DatabaseEntityMap map) => BuildInsertSql(map);

        public string UpdateSql(DatabaseEntityMap map) => BuildUpdateSql(map);

        public string DeleteSql(DatabaseEntityMap map) => BuildDeleteSql(map);

        public override bool IsTableExists(string tableName) => false;

        public override Task<bool> IsTableExistsAsync(
            string tableName,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        protected override IDbConnection CreateConnection() =>
            throw new NotSupportedException("Unit tests do not open a database connection.");

        protected override string QuoteIdentifier(string identifier) => $"[{identifier}]";

        protected override string GetSqlType(DatabasePropertyMap property) =>
            property.PropertyType == typeof(string) ? "TEXT" : "INTEGER";
    }

    private sealed class SqliteTestProvider : DatabaseProviderBase
    {
        public SqliteTestProvider(string connectionString)
            : base(connectionString)
        {
        }

        public override DatabaseProviderKind Kind => DatabaseProviderKind.Sqlite;

        public override bool IsTableExists(string tableName) =>
            ExecuteScalar<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @TableName",
                new { TableName = tableName }) > 0;

        public override async Task<bool> IsTableExistsAsync(
            string tableName,
            CancellationToken cancellationToken = default) =>
            await ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @TableName",
                new { TableName = tableName },
                cancellationToken) > 0;

        protected override IDbConnection CreateConnection() => new SqliteConnection(ConnectionString);

        protected override string QuoteIdentifier(string identifier) => $"[{identifier}]";

        protected override string GetSqlType(DatabasePropertyMap property) =>
            property.PropertyType == typeof(string) ? "TEXT" : "INTEGER";
    }
}
