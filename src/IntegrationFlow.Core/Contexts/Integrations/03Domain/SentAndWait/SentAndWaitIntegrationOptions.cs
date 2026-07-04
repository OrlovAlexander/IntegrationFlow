using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Глобальные опции интеграции SentAndWait.
    /// </summary>
    public static class SentAndWaitIntegrationOptions
    {
        /// <summary>
        /// Бросать исключение из <see cref="SentAndWaitIntegration.Integrate"/> при ошибке транспорта.
        /// По умолчанию <c>false</c> для обратной совместимости.
        /// </summary>
        public static bool ThrowOnFailure { get; set; }

        /// <summary>
        /// Повторять RPC после timeout, если задан <see cref="TransmitData.MessageId"/>.
        /// По умолчанию <c>false</c>.
        /// </summary>
        public static bool RetryOnTimeout { get; set; }

        /// <summary>
        /// Число повторов после timeout (не включая первую попытку). По умолчанию <c>1</c>.
        /// </summary>
        public static int MaxRetries { get; set; } = 1;

        /// <summary>
        /// Задержка перед повтором после timeout.
        /// </summary>
        public static TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    }
}
