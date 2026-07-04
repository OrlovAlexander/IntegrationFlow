using System;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending;

/// <summary>
/// EF Core staging для async RPC pending в scoped DbContext.
/// </summary>
public sealed class EfRpcPendingEnqueue<TContext> : IRpcPendingEnqueue
    where TContext : DbContext
{
    private readonly TContext context;

    public EfRpcPendingEnqueue(TContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Stage(RpcPendingRequest request)
    {
        context.Set<RpcPendingRequestEntity>().Add(EfRpcPendingMapper.ToEntity(request));
    }
}
