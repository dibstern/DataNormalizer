---
name: testcontainers
description: .NET Testcontainers patterns for integration tests in the DataNormalizer project. Covers container lifecycle management, database containers, wait strategies, and NUnit 4 integration with OneTimeSetUp/OneTimeTearDown.
---

# .NET Testcontainers

## Overview

Testcontainers is a library for creating lightweight, throwaway Docker containers for integration tests. While DataNormalizer doesn't require database containers for its core functionality, these patterns are useful for integration tests that verify normalization of data retrieved from real data sources.

## NuGet Package

Add to `Directory.Packages.props`:

```xml
<ItemGroup Label="Testing">
  <PackageVersion Include="Testcontainers" Version="4.3.0" />
  <PackageVersion Include="Testcontainers.PostgreSql" Version="4.3.0" />
  <PackageVersion Include="Testcontainers.MsSql" Version="4.3.0" />
</ItemGroup>
```

Add to test project `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Testcontainers.PostgreSql" />
</ItemGroup>
```

## NUnit 4 Integration

### Container Lifecycle with OneTimeSetUp/OneTimeTearDown

Use `[OneTimeSetUp]` and `[OneTimeTearDown]` to manage container lifecycle at the fixture level. Containers are expensive to start, so share them across tests in a fixture.

```csharp
[TestFixture]
public sealed class DatabaseIntegrationTests
{
    private PostgreSqlContainer _postgres = null!;
    private string _connectionString = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgres.DisposeAsync();
    }

    [Test]
    public async Task Normalize_DataFromPostgres_ProducesExpectedResult()
    {
        // Arrange - seed data
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        // ... seed test data

        // Act - read and normalize
        var data = await ReadPersonsAsync(connection);
        var result = TestNormalization.Normalize(data.First());

        // Assert
        Assert.That(result.PersonList, Has.Length.GreaterThan(result.RootIndex));
    }
}
```

### Container Per Test (When Isolation Required)

For tests that need a clean database:

```csharp
[TestFixture]
public sealed class IsolatedDatabaseTests
{
    [Test]
    public async Task Test_WithCleanDatabase()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await postgres.StartAsync();

        // Use postgres.GetConnectionString()
        // Container is disposed after the test
    }
}
```

## Database Containers

### PostgreSQL

```csharp
var container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .WithDatabase("testdb")
    .WithUsername("test")
    .WithPassword("test")
    .WithPortBinding(5432, true)  // random host port
    .Build();

await container.StartAsync();
var connectionString = container.GetConnectionString();
```

### SQL Server

```csharp
var container = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .WithPassword("Strong_password_123!")
    .Build();

await container.StartAsync();
var connectionString = container.GetConnectionString();
```

## Wait Strategies

Testcontainers has built-in wait strategies, but you can customize them:

### Default Wait (Port + Health Check)

The builder containers (PostgreSqlBuilder, etc.) include appropriate wait strategies by default. They wait for the database to accept connections.

### Custom Wait Strategy

```csharp
var container = new ContainerBuilder()
    .WithImage("custom-service:latest")
    .WithPortBinding(8080, true)
    .WithWaitStrategy(
        Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r
                .ForPath("/health")
                .ForPort(8080)
                .ForStatusCode(System.Net.HttpStatusCode.OK)))
    .Build();
```

### Wait with Timeout

```csharp
var container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .WithStartupCallback((container, ct) =>
    {
        // Additional initialization after container starts
        return Task.CompletedTask;
    })
    .Build();

// StartAsync has a default timeout; you can pass CancellationToken for custom
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
await container.StartAsync(cts.Token);
```

## Generic Container

For services without a dedicated builder:

```csharp
var container = new ContainerBuilder()
    .WithImage("redis:7-alpine")
    .WithPortBinding(6379, true)
    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
    .Build();

await container.StartAsync();
var host = container.Hostname;
var port = container.GetMappedPublicPort(6379);
```

## Shared Container Across Fixtures

For expensive containers shared across multiple test fixtures:

```csharp
// Shared fixture
[SetUpFixture]
public sealed class SharedDatabaseFixture
{
    public static PostgreSqlContainer Postgres { get; private set; } = null!;
    public static string ConnectionString { get; private set; } = "";

    [OneTimeSetUp]
    public async Task GlobalSetUp()
    {
        Postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await Postgres.StartAsync();
        ConnectionString = Postgres.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task GlobalTearDown()
    {
        await Postgres.DisposeAsync();
    }
}

// Test fixture using the shared container
[TestFixture]
public sealed class PersonNormalizationTests
{
    private string ConnectionString => SharedDatabaseFixture.ConnectionString;

    [Test]
    public async Task Test1()
    {
        // Use ConnectionString
    }
}
```

## Best Practices

1. **Use Alpine images** when available — smaller download, faster startup
2. **Use `[OneTimeSetUp]`/`[OneTimeTearDown]`** for container lifecycle — don't start/stop per test
3. **Use random port binding** (`WithPortBinding(5432, true)`) — avoids port conflicts
4. **Always `await DisposeAsync()`** — containers are not cleaned up on GC
5. **Set reasonable timeouts** — CI environments may be slower
6. **Use `[Category("Integration")]`** to separate from unit tests:
   ```csharp
   [TestFixture]
   [Category("Integration")]
   public sealed class DatabaseTests { }
   ```
   Run with: `dotnet test --filter "Category=Integration"`

## Docker Requirements

Testcontainers requires Docker to be running. In CI:

```yaml
# GitHub Actions
services:
  # Docker is available by default on ubuntu-latest
jobs:
  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: dotnet test --filter "Category=Integration"
```
