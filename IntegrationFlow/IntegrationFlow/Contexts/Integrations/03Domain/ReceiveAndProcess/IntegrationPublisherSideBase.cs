using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    internal abstract class IntegrationPublisherSideBase
    {
        /// <summary>
        /// Публикатор сообщений, запросов и т.п.
        /// </summary>
        public PublisherBase Publisher { get; internal set; }

        /// <summary>
        /// Ctor
        /// </summary>
        protected IntegrationPublisherSideBase()
        {
        }

        /// <summary>
        /// Возвращает конфигурацию публикатора, слушателя
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов, и т.п.</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract IConfiguration GetConfiguration(PublisherBase publisher, ILogger logger);

        /// <summary>
        /// Возвращает слушателя сообщений, запросов, и т.п.
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов, и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract ListenerBase GetListener(PublisherBase publisher, IConfiguration configuration, ILogger logger);

        /// <summary>
        /// Возвращает обработчик сообщений, запросов, и т.п.
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов, и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract ProcessorBase GetProcessor(PublisherBase publisher, IConfiguration configuration, ILogger logger);
    }
}
