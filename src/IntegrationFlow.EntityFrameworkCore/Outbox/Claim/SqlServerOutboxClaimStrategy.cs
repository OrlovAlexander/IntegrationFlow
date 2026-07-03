using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IntegrationFlow.EntityFrameworkCore.Outbox.Claim;

internal sealed class SqlServerOutboxClaimStrategy : IOutboxClaimStrategy
{
    internal const string TableName = "IntegrationFlowOutboxMessages";

    internal static readonly SqlServerOutboxClaimStrategy Instance = new();

    private SqlServerOutboxClaimStrategy()
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
        var ids = await SelectClaimableIdsAsync(context, batchSize, now, cancellationToken).ConfigureAwait(false);
        if (ids.Count == 0)
        {
            return Array.Empty<OutboxMessageEntity>();
        }

        var entities = await set.Where(entity => ids.Contains(entity.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        OutboxClaimHelper.MarkClaimed(entities, workerId, lockUntil);
        return entities;
    }

    private static async Task<List<Guid>> SelectClaimableIdsAsync(
        DbContext context,
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP (@batch) [Id]
            FROM [{TableName}] WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE [Status] = @status
              AND ([RetryAfter] IS NULL OR [RetryAfter] <= @now)
            ORDER BY [CreatedAt]
            """;

        var transaction = context.Database.CurrentTransaction;
        if (transaction != null)
        {
            command.Transaction = transaction.GetDbTransaction();
        }

        AddParameter(command, "@status", (int)OutboxMessageStatus.Pending);
        AddParameter(command, "@now", now);
        AddParameter(command, "@batch", Math.Max(1, batchSize));

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
