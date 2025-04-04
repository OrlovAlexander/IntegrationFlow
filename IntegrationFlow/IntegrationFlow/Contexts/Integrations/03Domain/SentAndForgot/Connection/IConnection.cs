using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection
{
    /// <summary>
    /// Подключение к противоположной стороне интеграции
    /// </summary>
    public interface IConnection : IDisposable
    {
        ///// <summary>
        ///// Открыть подключение
        ///// </summary>
        //bool Open();

        /// <summary>
        /// Проверить необходимость переподключения
        /// </summary>
        bool NeedReconnect();

        /// <summary>
        /// Переподключение
        /// </summary>
        bool Reconnect();
    }
}
