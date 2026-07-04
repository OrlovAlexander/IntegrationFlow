using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply
{
    /// <summary>
    /// Server-side pipeline для идемпотентного RPC: dedup + response cache + reply до ack.
    /// </summary>
    public static class RabbitMqRpcServerPipeline
    {
        private static readonly TimeSpan InProgressPollInterval = TimeSpan.FromMilliseconds(100);
        private const int InProgressPollAttempts = 20;

        /// <summary>
        /// Обрабатывает RPC-запрос с опциональным кешем ответов по MessageId.
        /// </summary>
        public static async Task HandleAsync(
            RabbitMqReceivedMessage request,
            RabbitMqReplyPublisher replyPublisher,
            Func<RabbitMqReceivedMessage, Task<string>> buildResponseAsync,
            IRequestReplyResponseStore? responseStore = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (replyPublisher == null)
            {
                throw new ArgumentNullException(nameof(replyPublisher));
            }

            if (buildResponseAsync == null)
            {
                throw new ArgumentNullException(nameof(buildResponseAsync));
            }

            if (!request.IsRequestReply)
            {
                return;
            }

            if (responseStore == null || string.IsNullOrWhiteSpace(request.MessageId))
            {
                var responseText = await buildResponseAsync(request).ConfigureAwait(false);
                replyPublisher.PublishTextReply(request, responseText);
                return;
            }

            var beginResult = await responseStore
                .TryBeginAsync(request.MessageId, cancellationToken)
                .ConfigureAwait(false);

            switch (beginResult)
            {
                case RequestReplyCacheResult.AlreadyProcessed:
                    await PublishCachedResponseAsync(request, replyPublisher, responseStore, cancellationToken)
                        .ConfigureAwait(false);
                    return;

                case RequestReplyCacheResult.InProgress:
                    var cachedWhileWaiting = await WaitForCachedResponseAsync(
                            responseStore,
                            request.MessageId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (cachedWhileWaiting != null)
                    {
                        replyPublisher.PublishReply(request.ReplyTo, request.CorrelationId, cachedWhileWaiting);
                        return;
                    }

                    throw new InvalidOperationException(
                        $"RPC request '{request.MessageId}' is still in progress.");
            }

            var response = await buildResponseAsync(request).ConfigureAwait(false);
            var responseBody = Encoding.UTF8.GetBytes(response ?? string.Empty);
            await responseStore
                .StoreResponseAsync(request.MessageId, responseBody, cancellationToken)
                .ConfigureAwait(false);
            replyPublisher.PublishReply(request.ReplyTo, request.CorrelationId, responseBody);
        }

        private static async Task PublishCachedResponseAsync(
            RabbitMqReceivedMessage request,
            RabbitMqReplyPublisher replyPublisher,
            IRequestReplyResponseStore responseStore,
            CancellationToken cancellationToken)
        {
            var cached = await responseStore
                .GetCachedResponseAsync(request.MessageId, cancellationToken)
                .ConfigureAwait(false);
            if (cached == null || cached.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Cached RPC response for MessageId '{request.MessageId}' was not found.");
            }

            replyPublisher.PublishReply(request.ReplyTo, request.CorrelationId, cached);
        }

        private static async Task<byte[]?> WaitForCachedResponseAsync(
            IRequestReplyResponseStore responseStore,
            string messageId,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < InProgressPollAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cached = await responseStore
                    .GetCachedResponseAsync(messageId, cancellationToken)
                    .ConfigureAwait(false);
                if (cached != null)
                {
                    return cached;
                }

                await Task.Delay(InProgressPollInterval, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }
    }
}
