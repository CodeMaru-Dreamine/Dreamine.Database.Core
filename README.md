# Dreamine.Database.Core

[![CI](https://github.com/CodeMaru-Dreamine/Dreamine.Database.Core/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/CodeMaru-Dreamine/Dreamine.Database.Core/actions/workflows/ci.yml?query=branch%3Amain) [![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=CodeMaru-Dreamine_Dreamine.Database.Core&metric=alert_status&branch=main)](https://sonarcloud.io/summary/new_code?id=CodeMaru-Dreamine_Dreamine.Database.Core&branch=main) [![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=CodeMaru-Dreamine_Dreamine.Database.Core&metric=security_rating&branch=main)](https://sonarcloud.io/summary/new_code?id=CodeMaru-Dreamine_Dreamine.Database.Core&branch=main) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=CodeMaru-Dreamine_Dreamine.Database.Core&metric=coverage&branch=main)](https://sonarcloud.io/summary/new_code?id=CodeMaru-Dreamine_Dreamine.Database.Core&branch=main)<br>
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE) ![.NET](https://img.shields.io/badge/.NET-8-512BD4.svg?logo=dotnet&logoColor=white) [![NuGet](https://img.shields.io/nuget/v/Dreamine.Database.Core.svg)](https://www.nuget.org/packages/Dreamine.Database.Core) [![NuGet Downloads](https://img.shields.io/nuget/dt/Dreamine.Database.Core.svg)](https://www.nuget.org/packages/Dreamine.Database.Core)<br>
[![Docs](https://img.shields.io/badge/%F0%9F%93%96%20Docs-dreamine.kr-49B2FF.svg)](https://dreamine.kr/libraries?lang=en) [![Guide](https://img.shields.io/badge/%F0%9F%93%98%20Guide-dreamine.kr-49B2FF.svg)](https://dreamine.kr/guide?lang=en) [![Playground](https://img.shields.io/badge/%F0%9F%8E%AE%20Playground-dreamine.kr-49B2FF.svg)](https://dreamine.kr/playground?lang=en) [![Book](https://img.shields.io/badge/%F0%9F%93%96%20Book-Practical%20MVVM%20Architecture-000000.svg)](https://bookk.co.kr/bookStore/69c0f1b41461ec1ae849a0f6)

`Dreamine.Database.Core` provides the shared runtime implementation used by concrete Dreamine database providers.

[한국어 문서](./README_KO.md)

## Package Role

This package implements provider-independent CRUD, SQL generation, entity mapping, Dapper integration, and database-provider base behavior.

```text
Dreamine.Database.Abstractions
        ↑
Dreamine.Database.Core
        ↑
SQLite / MySQL / Oracle / SQL Server providers
```

Concrete providers supply connection creation, identifier quoting, provider-specific SQL types, and table creation dialects.

## Features

- Attribute-based entity map generation
- Common `DatabaseProviderBase`
- Dapper-backed command, scalar, query, insert, update, and delete operations
- Provider-specific extension points for SQL type mapping and identifier quoting
- Guarded `CreateTable<T>()` flow that skips existing tables
- Sync and async implementations for the common provider contract

## Provider Extension Points

| Member | Purpose |
|---|---|
| `CreateConnection()` | Creates the concrete ADO.NET connection. |
| `QuoteIdentifier(string)` | Quotes table and column names for the provider dialect. |
| `GetSqlType(DatabasePropertyMap)` | Maps CLR property types to provider SQL types. |
| `BuildCreateTableSql(DatabaseEntityMap)` | Builds provider-specific table creation SQL. |
| `ParameterPrefix` | Selects parameter prefix such as `@` or `:`. |

## Design Principles

- Keep shared CRUD behavior in one place.
- Keep vendor drivers out of the core package.
- Keep SQL dialect differences inside concrete providers.
- Preserve the same application-facing API for every provider.

## Dependencies

- `Dreamine.Database.Abstractions`
- `Dapper`

## Target Framework

```text
net8.0
```

## Related Packages

- `Dreamine.Database.Abstractions`
- `Dreamine.Database.Sqlite`
- `Dreamine.Database.MySql`
- `Dreamine.Database.Oracle`
- `Dreamine.Database.SqlServer`

## Samples and Tests

- Unit tests: `20_SOURCES/200. Tests/Dreamine.FullKit.Tests/Database`
- WPF sample: `20_SOURCES/998. DEMO/000. Sample/010. Wpfs/SampleSmart/Pages/PageSub/PageDatabase.xaml`

## License

MIT License
