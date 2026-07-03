namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot
{
    /// <summary>
    /// Результат выполнения интеграции SentAndForgot.
    /// </summary>
    public sealed class IntegrateResult
    {
        /// <summary>
        /// Успешно ли выполнена интеграция.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Причина ошибки при неуспешном выполнении.
        /// </summary>
        public string FailureReason { get; set; } = string.Empty;

        /// <summary>
        /// Идентификатор сообщения (MessageId).
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Создаёт успешный результат.
        /// </summary>
        public static IntegrateResult Succeeded(string messageId)
            => new IntegrateResult { Success = true, MessageId = messageId ?? string.Empty };

        /// <summary>
        /// Создаёт неуспешный результат.
        /// </summary>
        public static IntegrateResult Failed(string reason)
            => new IntegrateResult { Success = false, FailureReason = reason ?? string.Empty };
    }
}
