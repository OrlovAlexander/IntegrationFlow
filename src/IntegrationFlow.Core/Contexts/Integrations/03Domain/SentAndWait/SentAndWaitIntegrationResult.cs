using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Результат выполнения интеграции SentAndWait.
    /// </summary>
    public sealed class SentAndWaitIntegrationResult
    {
        private SentAndWaitIntegrationResult(
            bool success,
            ObtainedData data,
            string failureReason,
            bool timedOut,
            Exception? exception)
        {
            Success = success;
            Data = data;
            FailureReason = failureReason ?? string.Empty;
            TimedOut = timedOut;
            Exception = exception;
        }

        /// <summary>
        /// Успешно ли выполнена интеграция.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Полученные данные.
        /// </summary>
        public ObtainedData Data { get; }

        /// <summary>
        /// Причина ошибки при неуспешном выполнении.
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// Истек ли таймаут ожидания ответа.
        /// </summary>
        public bool TimedOut { get; }

        /// <summary>
        /// Исключение при ошибке транспорта или обработки.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Создаёт успешный результат.
        /// </summary>
        public static SentAndWaitIntegrationResult Succeeded(ObtainedData data)
            => new(true, data, string.Empty, timedOut: false, exception: null);

        /// <summary>
        /// Создаёт неуспешный результат.
        /// </summary>
        public static SentAndWaitIntegrationResult Failed(string reason, Exception? exception = null)
            => new(false, new ObtainedData(null, isFailed: true), reason, timedOut: false, exception);

        /// <summary>
        /// Создаёт результат по таймауту ожидания ответа.
        /// </summary>
        public static SentAndWaitIntegrationResult Timeout(string reason, Exception? exception = null)
            => new(false, new ObtainedData(null, isFailed: true), reason, timedOut: true, exception);
    }
}
