using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Outbox.Claim;

internal static class OutboxClaimStrategyResolver
{
    internal static IOutboxClaimStrategy Resolve(DbContext context)
    {
        var provider = context.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return PostgreSqlOutboxClaimStrategy.Instance;
        }

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return SqlServerOutboxClaimStrategy.Instance;
        }

        return SqliteOutboxClaimStrategy.Instance;
    }
}

internal static class OutboxClaimHelper
{
    internal static void MarkClaimed(
        IEnumerable<OutboxMessageEntity> entities,
        string workerId,
        DateTimeOffset lockUntil)
    {
        foreach (var entity in entities)
        {
            entity.Status = OutboxMessageStatus.InFlight;
            entity.LockedBy = workerId;
            entity.LockedUntil = lockUntil;
        }
    }

    internal static async Task<List<OutboxMessageEntity>> SelectPendingCandidatesAsync(
        DbSet<OutboxMessageEntity> set,
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pending = await set
            .Where(message => message.Status == OutboxMessageStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return pending
            .Where(message => message.RetryAfter == null || message.RetryAfter <= now)
            .OrderBy(message => message.CreatedAt)
            .Take(Math.Max(1, batchSize))
            .ToList();
    }
}
