using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot
{
    /// <summary>
    /// Интеграция SentAndForgot завершилась с ошибкой.
    /// </summary>
    public sealed class IntegrationFailedException : Exception
    {
        public IntegrationFailedException(string reason)
            : base(reason ?? string.Empty)
        {
            FailureReason = reason ?? string.Empty;
        }

        /// <summary>
        /// Причина ошибки.
        /// </summary>
        public string FailureReason { get; }
    }
}
