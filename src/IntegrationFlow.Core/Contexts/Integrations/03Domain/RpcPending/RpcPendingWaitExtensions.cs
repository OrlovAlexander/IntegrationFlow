using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Ожидание завершения async RPC pending request.
    /// </summary>
    public static class RpcPendingWaitExtensions
    {
        private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Polls store until request reaches terminal state or timeout.
        /// </summary>
        public static async Task<RpcPendingRequest?> WaitForCompletionAsync(
            this IRpcPendingStore store,
            Guid id,
            TimeSpan timeout,
            CancellationToken cancellationToken = default,
            TimeSpan? pollInterval = null)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            var interval = pollInterval ?? DefaultPollInterval;
            var deadline = DateTimeOffset.UtcNow.Add(timeout);

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = await store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                if (current == null)
                {
                    return null;
                }

                if (current.Status is RpcPendingStatus.Completed or RpcPendingStatus.Failed or RpcPendingStatus.TimedOut)
                {
                    return current;
                }

                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }

            await store.MarkTimedOutAsync(id, "Pending response timeout.", cancellationToken).ConfigureAwait(false);
            return await store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }
}
