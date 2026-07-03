using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

internal sealed class TestDbContextFactory : IDbContextFactory<TestIntegrationDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<TestIntegrationDbContext> options;

    private TestDbContextFactory(SqliteConnection connection, DbContextOptions<TestIntegrationDbContext> options)
    {
        this.connection = connection;
        this.options = options;
    }

    public static TestDbContextFactory Create(string databaseName)
    {
        var connection = new SqliteConnection($"Data Source={databaseName};Mode=Memory;Cache=Shared");
        connection.Open();

        var options = new DbContextOptionsBuilder<TestIntegrationDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new TestIntegrationDbContext(options);
        context.Database.EnsureCreated();

        return new TestDbContextFactory(connection, options);
    }

    public TestIntegrationDbContext CreateDbContext()
        => new(options);

    public Task<TestIntegrationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public ValueTask DisposeAsync()
    {
        connection.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal static class TestDbContextFactoryFactory
{
    public static TestDbContextFactory Create(string databaseName)
        => TestDbContextFactory.Create(databaseName);
}
