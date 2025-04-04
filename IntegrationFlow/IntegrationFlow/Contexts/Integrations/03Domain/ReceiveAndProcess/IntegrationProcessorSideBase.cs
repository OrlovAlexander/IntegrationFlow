using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Validator;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Сторона обработчика входного сообщения, запроса и т.п.
    /// </summary>
    internal abstract class IntegrationProcessorSideBase
    {
        /// <summary>
        /// Обработчик входного сообщения, запроса и т.п.
        /// </summary>
        public ProcessorBase Processor { get; internal set; }

        /// <summary>
        /// Ctor
        /// </summary>
        public IntegrationProcessorSideBase()
        {
        }

        /// <summary>
        /// Возвращает валидатор входного сообщения, запроса и т.п.
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract IValidator GetValidator(PublisherBase publisher, IConfiguration configuration, ILogger logger);

        /// <summary>
        /// Возвращает логгер для логирования входного сообщения, запроса и т.п.
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract ILogging GetLogging(PublisherBase publisher, IConfiguration configuration, ILogger logger);

        /// <summary>
        /// Возвращает метод обработки входного сообщения, запроса и т.п. не прошедшего проверку
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract IInboxMessageFailedProcessing GetInboxMessageFailedProcessing(PublisherBase publisher, IConfiguration configuration, ILogger logger);

        /// <summary>
        /// Возвращает форматировщик входного сообщения, запроса и т.п.
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract IFormatterInboxMessage GetFormatterInboxMessage(PublisherBase publisher, IConfiguration configuration, ILogger logger);

        /// <summary>
        /// Возвращает метод обработки входного сообщения, запроса и т.п.
        /// </summary>
        /// <param name="publisher">Публикатор сообщений, запросов и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public abstract IInboxMessageProcessing GetInboxMessageProcessing(PublisherBase publisher, IConfiguration configuration, ILogger logger);
    }
}
