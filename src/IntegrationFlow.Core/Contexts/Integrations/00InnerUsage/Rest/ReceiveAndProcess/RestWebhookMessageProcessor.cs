#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using Microsoft.AspNetCore.Http;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;

/// <summary>
/// Processes inbound webhook HTTP requests with dedup, metrics and tracing.
/// </summary>
internal sealed class RestWebhookMessageProcessor
{
    public async Task<RestWebhookProcessResult> ProcessAsync(
        HttpContext httpContext,
        RestWebhookConfiguration configuration,
        Func<RestWebhookReceivedMessage, CancellationToken, Task> handler,
        IIntegrationLogger logger,
        IIntegrationFlowMetrics? metrics,
        IMessageDeduplicationStore? deduplicationStore,
        IRestWebhookAuthenticator? authenticator,
        CancellationToken cancellationToken = default)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        var request = httpContext.Request;
        if (!configuration.IsMethodAllowed(request.Method))
        {
            return RestWebhookProcessResult.MethodNotAllowed;
        }

        var readBodyResult = await ReadBodyAsync(request, configuration.MaxBodyBytes, cancellationToken)
            .ConfigureAwait(false);
        if (readBodyResult.Error != null)
        {
            return readBodyResult.Error.Value;
        }

        var headers = SnapshotHeaders(request.Headers);
        var messageId = GetHeaderValue(headers, configuration.MessageIdHeaderName);
        var correlationId = GetHeaderValue(headers, configuration.CorrelationIdHeaderName);

        if (configuration.RequireMessageId && string.IsNullOrWhiteSpace(messageId))
        {
            return RestWebhookProcessResult.MissingMessageId;
        }

        var message = new RestWebhookReceivedMessage(
            configuration.Name,
            readBodyResult.Body,
            messageId,
            correlationId,
            request.ContentType ?? string.Empty,
            request.Path.Value ?? string.Empty,
            headers,
            DateTimeOffset.UtcNow);

        using (RestDistributedTracing.StartConsumerActivity(
                   request.Headers,
                   "receive",
                   configuration.Name,
                   message.MessageId,
                   message.CorrelationId))
        {
            if (authenticator != null)
            {
                var authenticated = await authenticator
                    .TryAuthenticateAsync(httpContext, configuration, message, cancellationToken)
                    .ConfigureAwait(false);
                if (!authenticated)
                {
                    return RestWebhookProcessResult.Unauthorized;
                }
            }

            var dedupResult = await TryBeginDeduplicationAsync(
                    deduplicationStore,
                    messageId,
                    configuration.Name,
                    logger,
                    metrics,
                    cancellationToken)
                .ConfigureAwait(false);

            if (dedupResult.HasValue)
            {
                return dedupResult.Value;
            }

            var processingAcquired = deduplicationStore != null && !string.IsNullOrWhiteSpace(messageId);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await handler(message, cancellationToken).ConfigureAwait(false);
                metrics?.RecordMessageProcessed(configuration.Name, stopwatch.Elapsed, success: true);

                if (processingAcquired)
                {
                    await deduplicationStore!
                        .MarkProcessedAsync(messageId, cancellationToken)
                        .ConfigureAwait(false);
                    processingAcquired = false;
                }

                return RestWebhookProcessResult.Success;
            }
            catch (Exception ex)
            {
                metrics?.RecordMessageProcessed(configuration.Name, stopwatch.Elapsed, success: false);
                metrics?.RecordConsumerOutcome(configuration.Name, ConsumerOutcomeReason.Requeue);
                logger.LogException(SR.T("REST webhook. Ошибка обработки сообщения."), ex);
                return RestWebhookProcessResult.HandlerFailed;
            }
            finally
            {
                if (processingAcquired &&
                    deduplicationStore != null &&
                    !string.IsNullOrWhiteSpace(messageId))
                {
                    await deduplicationStore
                        .ReleaseProcessingAsync(messageId, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task<RestWebhookProcessResult?> TryBeginDeduplicationAsync(
        IMessageDeduplicationStore? deduplicationStore,
        string messageId,
        string profileName,
        IIntegrationLogger logger,
        IIntegrationFlowMetrics? metrics,
        CancellationToken cancellationToken)
    {
        if (deduplicationStore == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            logger.LogWarn(SR.T(
                "Dedup store настроен, но MessageId отсутствует — идемпотентность пропущена."));
            return null;
        }

        var beginResult = await deduplicationStore
            .TryBeginProcessingAsync(messageId, cancellationToken)
            .ConfigureAwait(false);

        switch (beginResult)
        {
            case DeduplicationBeginResult.AlreadyProcessed:
                using (IntegrationStructuredLogging.BeginScope(
                           logger,
                           (IntegrationStructuredLogFields.Profile, profileName),
                           (IntegrationStructuredLogFields.MessageId, messageId),
                           (IntegrationStructuredLogFields.Kind, "webhook"),
                           (IntegrationStructuredLogFields.Outcome, "dedup_skip")))
                {
                    logger.LogInfo(SR.T("Webhook уже обработан, пропуск."));
                }

                metrics?.RecordConsumerOutcome(profileName, ConsumerOutcomeReason.DedupSkip);
                return RestWebhookProcessResult.DuplicateSkipped;
            case DeduplicationBeginResult.InProgress:
                metrics?.RecordConsumerOutcome(profileName, ConsumerOutcomeReason.InProgressRequeue);
                return RestWebhookProcessResult.InProgress;
            case DeduplicationBeginResult.Acquired:
                return null;
            default:
                return null;
        }
    }

    private static async Task<(byte[] Body, RestWebhookProcessResult? Error)> ReadBodyAsync(
        HttpRequest request,
        int maxBodyBytes,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength.HasValue && request.ContentLength.Value > maxBodyBytes)
        {
            return (Array.Empty<byte>(), RestWebhookProcessResult.PayloadTooLarge);
        }

        request.EnableBuffering();

        using var memoryStream = new MemoryStream();
        var buffer = new byte[8192];
        var totalBytes = 0;

        while (true)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > maxBodyBytes)
            {
                return (Array.Empty<byte>(), RestWebhookProcessResult.PayloadTooLarge);
            }

            memoryStream.Write(buffer, 0, read);
        }

        request.Body.Position = 0;
        return (memoryStream.ToArray(), null);
    }

    private static IReadOnlyDictionary<string, string> SnapshotHeaders(IHeaderDictionary headers)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            snapshot[header.Key] = header.Value.ToString();
        }

        return snapshot;
    }

    private static string GetHeaderValue(IReadOnlyDictionary<string, string> headers, string headerName)
    {
        if (headers.TryGetValue(headerName, out var value))
        {
            return value;
        }

        return headers
            .FirstOrDefault(pair => string.Equals(pair.Key, headerName, StringComparison.OrdinalIgnoreCase))
            .Value ?? string.Empty;
    }
}
#endif
