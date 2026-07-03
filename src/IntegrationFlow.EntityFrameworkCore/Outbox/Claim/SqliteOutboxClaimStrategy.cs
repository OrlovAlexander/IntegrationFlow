using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Outbox.Claim;

internal sealed class SqliteOutboxClaimStrategy : IOutboxClaimStrategy
{
    internal static readonly SqliteOutboxClaimStrategy Instance = new();

    private SqliteOutboxClaimStrategy()
    {
    }

    public async Task<IReadOnlyList<OutboxMessageEntity>> ClaimAsync(
        DbContext context,
        DbSet<OutboxMessageEntity> set,
        int batchSize,
        string workerId,
        DateTimeOffset lockUntil,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = await OutboxClaimHelper
            .SelectPendingCandidatesAsync(set, batchSize, now, cancellationToken)
            .ConfigureAwait(false);

        OutboxClaimHelper.MarkClaimed(candidates, workerId, lockUntil);
        return candidates;
    }
}
