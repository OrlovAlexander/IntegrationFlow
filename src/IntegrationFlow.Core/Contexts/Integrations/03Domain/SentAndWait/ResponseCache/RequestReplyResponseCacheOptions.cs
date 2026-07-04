using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache
{
    /// <summary>
    /// Опции хранения кешированных RPC-ответов.
    /// </summary>
    public sealed class RequestReplyResponseCacheOptions
    {
        /// <summary>
        /// Срок хранения ответа после завершения обработки.
        /// </summary>
        public TimeSpan ResponseRetention { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// Максимальное время удержания lock «in progress» до повторной попытки.
        /// </summary>
        public TimeSpan ProcessingLockDuration { get; set; } = TimeSpan.FromMinutes(15);
    }
}
