using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending.Claim;

internal static class RpcPendingClaimHelper
{
    internal static void MarkClaimed(
        IEnumerable<RpcPendingRequestEntity> entities,
        string workerId,
        DateTimeOffset lockUntil)
    {
        foreach (var entity in entities)
        {
            entity.Status = RpcPendingStatus.InFlight;
            entity.LockedBy = workerId;
            entity.LockedUntil = lockUntil;
        }
    }

    internal static async Task<List<RpcPendingRequestEntity>> SelectPendingCandidatesAsync(
        DbSet<RpcPendingRequestEntity> set,
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pending = await set
            .Where(request => request.Status == RpcPendingStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return pending
            .Where(request => request.RetryAfter == null || request.RetryAfter <= now)
            .OrderBy(request => request.CreatedAt)
            .Take(Math.Max(1, batchSize))
            .ToList();
    }
}
