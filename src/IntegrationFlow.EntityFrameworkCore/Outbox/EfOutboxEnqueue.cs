using System;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Outbox;

/// <summary>
/// EF Core staging outbox в scoped <see cref="DbContext"/> без SaveChanges.
/// </summary>
public sealed class EfOutboxEnqueue<TContext> : IOutboxEnqueue
    where TContext : DbContext
{
    private readonly TContext context;

    public EfOutboxEnqueue(TContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public void Stage(OutboxMessage message)
    {
        context.Set<OutboxMessageEntity>().Add(EfOutboxMapper.ToEntity(message));
    }
}
