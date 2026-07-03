using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Outbox.Claim;

internal interface IOutboxClaimStrategy
{
    Task<IReadOnlyList<OutboxMessageEntity>> ClaimAsync(
        DbContext context,
        DbSet<OutboxMessageEntity> set,
        int batchSize,
        string workerId,
        DateTimeOffset lockUntil,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
