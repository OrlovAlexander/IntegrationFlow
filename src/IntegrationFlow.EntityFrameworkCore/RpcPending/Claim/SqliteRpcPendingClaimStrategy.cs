using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending.Claim;

internal sealed class SqliteRpcPendingClaimStrategy : IRpcPendingClaimStrategy
{
    internal static readonly SqliteRpcPendingClaimStrategy Instance = new();

    private SqliteRpcPendingClaimStrategy()
    {
    }

    public async Task<IReadOnlyList<RpcPendingRequestEntity>> ClaimAsync(
        DbContext context,
        DbSet<RpcPendingRequestEntity> set,
        int batchSize,
        string workerId,
        DateTimeOffset lockUntil,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = await RpcPendingClaimHelper
            .SelectPendingCandidatesAsync(set, batchSize, now, cancellationToken)
            .ConfigureAwait(false);

        RpcPendingClaimHelper.MarkClaimed(candidates, workerId, lockUntil);
        return candidates;
    }
}
