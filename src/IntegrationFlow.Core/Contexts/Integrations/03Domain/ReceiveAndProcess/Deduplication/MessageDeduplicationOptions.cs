using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication
{
    /// <summary>
    /// Настройки хранения dedup-записей.
    /// </summary>
    public sealed class MessageDeduplicationOptions
    {
        /// <summary>
        /// Срок хранения обработанных message id. Null — без автоочистки.
        /// </summary>
        public TimeSpan? ProcessedRetention { get; set; }
    }
}
