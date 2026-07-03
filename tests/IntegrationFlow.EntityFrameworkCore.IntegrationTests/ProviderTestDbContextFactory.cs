using IntegrationFlow.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IntegrationFlow.EntityFrameworkCore.IntegrationTests;

internal sealed class ProviderTestDbContextFactory : IDbContextFactory<TestIntegrationDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<TestIntegrationDbContext> options;

    private ProviderTestDbContextFactory(DbContextOptions<TestIntegrationDbContext> options)
    {
        this.options = options;
    }

    public static async Task<ProviderTestDbContextFactory> CreatePostgreSqlAsync(PostgreSqlContainer container)
    {
        var options = new DbContextOptionsBuilder<TestIntegrationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var context = new TestIntegrationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new ProviderTestDbContextFactory(options);
    }

    public static async Task<ProviderTestDbContextFactory> CreateSqlServerAsync(MsSqlContainer container)
    {
        var options = new DbContextOptionsBuilder<TestIntegrationDbContext>()
            .UseSqlServer(container.GetConnectionString())
            .Options;

        await using var context = new TestIntegrationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new ProviderTestDbContextFactory(options);
    }

    public TestIntegrationDbContext CreateDbContext()
        => new(options);

    public Task<TestIntegrationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
