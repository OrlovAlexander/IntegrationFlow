using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending.Claim;

internal interface IRpcPendingClaimStrategy
{
    Task<IReadOnlyList<RpcPendingRequestEntity>> ClaimAsync(
        DbContext context,
        DbSet<RpcPendingRequestEntity> set,
        int batchSize,
        string workerId,
        DateTimeOffset lockUntil,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
