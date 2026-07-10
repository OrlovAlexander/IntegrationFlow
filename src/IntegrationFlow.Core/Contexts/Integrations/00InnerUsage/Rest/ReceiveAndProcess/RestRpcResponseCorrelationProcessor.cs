#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;

/// <summary>
/// Completes async REST RPC pending requests from inbound callback webhooks.
/// </summary>
internal sealed class RestRpcResponseCorrelationProcessor
{
    public async Task<RestRpcResponseProcessResult> ProcessAsync(
        RestWebhookReceivedMessage message,
        RestRequestReplyConfiguration requestReplyConfiguration,
        IRpcPendingStore pendingStore,
        IIntegrationLogger logger,
        IIntegrationFlowMetrics? metrics,
        CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (requestReplyConfiguration == null)
        {
            throw new ArgumentNullException(nameof(requestReplyConfiguration));
        }

        if (pendingStore == null)
        {
            throw new ArgumentNullException(nameof(pendingStore));
        }

        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        var correlationValue = !string.IsNullOrWhiteSpace(message.CorrelationId)
            ? message.CorrelationId
            : message.MessageId;
        if (!Guid.TryParse(correlationValue, out var pendingId))
        {
            return RestRpcResponseProcessResult.InvalidCorrelationId;
        }

        var pending = await pendingStore.GetByIdAsync(pendingId, cancellationToken).ConfigureAwait(false);
        if (pending == null)
        {
            return RestRpcResponseProcessResult.PendingNotFound;
        }

        if (pending.Status == RpcPendingStatus.Completed)
        {
            return RestRpcResponseProcessResult.DuplicateSkipped;
        }

        if (pending.Status != RpcPendingStatus.AwaitingResponse)
        {
            return RestRpcResponseProcessResult.InvalidPendingState;
        }

        try
        {
            await pendingStore
                .CompleteAsync(pendingId, message.Body, cancellationToken)
                .ConfigureAwait(false);

            metrics?.RecordRpcPendingCompleted(
                requestReplyConfiguration.Name,
                DateTimeOffset.UtcNow - pending.CreatedAt,
                success: true);

            return RestRpcResponseProcessResult.Completed;
        }
        catch (Exception ex)
        {
            logger.LogException("REST async RPC response correlation failed.", ex);
            return RestRpcResponseProcessResult.HandlerFailed;
        }
    }
}
#endif
