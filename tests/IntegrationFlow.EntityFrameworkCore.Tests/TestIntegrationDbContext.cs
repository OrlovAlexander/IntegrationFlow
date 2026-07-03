using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.EntityFrameworkCore.Deduplication;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

internal sealed class TestIntegrationDbContext : DbContext
{
    public TestIntegrationDbContext(DbContextOptions<TestIntegrationDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<ProcessedMessageEntity> ProcessedMessages => Set<ProcessedMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureIntegrationFlow();
}
