using System;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending.Claim;

internal static class RpcPendingClaimStrategyResolver
{
    internal static IRpcPendingClaimStrategy Resolve(DbContext context)
    {
        var provider = context.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return PostgreSqlRpcPendingClaimStrategy.Instance;
        }

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return SqlServerRpcPendingClaimStrategy.Instance;
        }

        return SqliteRpcPendingClaimStrategy.Instance;
    }
}
