using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending;

/// <summary>
/// Stages compensation action into transactional outbox (SentAndForgot relay).
/// </summary>
public sealed class OutboxRpcCompensationHandler<TContext> : IRpcCompensationHandler
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> contextFactory;
    private readonly OutboxRpcCompensationOptions options;

    public OutboxRpcCompensationHandler(
        IDbContextFactory<TContext> contextFactory,
        OutboxRpcCompensationOptions options)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.OutboxProfileName))
        {
            throw new ArgumentException("Outbox profile name is required.", nameof(options));
        }
    }

    public async Task<bool> TryCompensateAsync(RpcPendingRequest request, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var enqueue = new EfOutboxEnqueue<TContext>(context);
        enqueue.Stage(new OutboxMessage(
            DeriveOutboxMessageId(request.Id),
            options.OutboxProfileName,
            BuildPayload(request),
            options.ContentType,
            DateTimeOffset.UtcNow,
            attemptCount: 0));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal static Guid DeriveOutboxMessageId(Guid pendingId)
    {
        var bytes = pendingId.ToByteArray();
        bytes[15] = (byte)(bytes[15] ^ 0xC0);
        return new Guid(bytes);
    }

    private byte[] BuildPayload(RpcPendingRequest request)
    {
        if (options.PayloadFactory != null)
        {
            return options.PayloadFactory(request);
        }

        var payload = JsonSerializer.Serialize(new
        {
            pendingId = request.Id,
            profileName = request.ProfileName,
            status = request.Status.ToString(),
            error = request.LastError,
            createdAt = request.CreatedAt
        });

        return Encoding.UTF8.GetBytes(payload);
    }
}

/// <summary>
/// Options for <see cref="OutboxRpcCompensationHandler{TContext}"/>.
/// </summary>
public sealed class OutboxRpcCompensationOptions
{
    public string OutboxProfileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/json";

    public Func<RpcPendingRequest, byte[]>? PayloadFactory { get; set; }
}
