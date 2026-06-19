using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot
{
    /// <summary>
    /// Противоположная сторона интеграции
    /// </summary>
    internal abstract class SentAndForgotIntegrationOppositeSide
    {
        /// <summary>
        /// Код противоположной стороны интеграции
        /// </summary>
        public object IntegrationOppositeSideCode
        {
            get
            {
                return GetIntegrationOppositeSideCode();
            }
        }

        /// <summary>
        /// Возвращает форматировщик для форматирования исходных данных
        /// </summary>
        /// <param name="logger">Логгер в рамках интеграций</param>
        /// <returns>Форматировщик</returns>
        public abstract IFormatterTransmitData GetFormatterSourceData(IIntegrationLogger logger);

        /// <summary>
        /// Возвращает конфигуратор способа обращения к противоположной стороне
        /// </summary>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract IConfiguration GetTransmitterConfiguration(IIntegrationLogger logger);

        /// <summary>
        /// Возвращает подключение к противоположной стороне
        /// </summary>
        /// <param name="configuration">Конфигуратор способа обращения к противоположной стороне</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract IConnection GetConnection(IConfiguration configuration, IIntegrationLogger logger);

        /// <summary>
        /// Возвращает способ обращения к противоположной стороне интеграции
        /// </summary>
        /// <param name="configuration">Конфигуратор способа обращения к противоположной стороне</param>
        /// <param name="connection">Подключение к противоположной стороне</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        /// <returns>Способ обращения к противоположной стороне интеграции</returns>
        public abstract ITransmitter GetTransmitter(IConfiguration configuration, IConnection connection, IIntegrationLogger logger);

        /// <summary>
        /// Возвращает логгер данных
        /// </summary>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract ILogging GetLogging(IIntegrationLogger logger);

        /// <summary>
        /// Возвращает код интеграции
        /// </summary>
        /// <returns></returns>
        protected abstract object GetIntegrationOppositeSideCode();
    }
}
