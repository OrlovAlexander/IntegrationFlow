using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection
{
    /// <summary>
    /// Пустое подключение для режимов без прямого транспорта (например, outbox).
    /// </summary>
    internal sealed class NullSentAndForgotConnection : IConnection
    {
        internal static readonly NullSentAndForgotConnection Instance = new NullSentAndForgotConnection();

        private NullSentAndForgotConnection()
        {
        }

        public bool NeedReconnect() => false;

        public bool Reconnect() => true;

        public void Dispose()
        {
        }
    }
}
