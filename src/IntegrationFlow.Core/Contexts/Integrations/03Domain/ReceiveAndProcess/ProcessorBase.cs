using System;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Обработчик входного сообщения, запроса и т.п.
    /// </summary>
    internal class ProcessorBase
    {
        /// <summary>
        /// Конфигурация публикатора, слушателя
        /// </summary>
        protected IConfiguration Configuration { get; private set; }

        /// <summary>
        /// Логгер в рамках интеграций
        /// </summary>
        protected IIntegrationLogger Logger { get; private set; }

        /// <summary>
        /// Публикатор сообщений, запросов и т.п.
        /// </summary>
        protected PublisherBase Publisher { get; private set; }

        /// <summary>
        /// Сторона обработчика входного сообщения, запроса и т.п.
        /// </summary>
        protected  IntegrationProcessorSideBase IntegrationProcessorSide { get; private set; }

        /// <summary>
        /// Ctor
        /// </summary>
        protected ProcessorBase()
        {
        }

        /// <summary>
        /// Создать обработчик входящего сообщения, запроса и т.п.
        /// </summary>
        /// <typeparam name="TProcessor">Тип обработчика</typeparam>
        /// <typeparam name="TIntegrationProcessorSide">Сторона обработчика входного сообщения, запроса и т.п.</typeparam>
        /// <param name="publisher">Публикатор сообщений, запросов и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        public static ProcessorBase Create<TProcessor, TIntegrationProcessorSide>(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
            where TProcessor : ProcessorBase, new()
            where TIntegrationProcessorSide : IntegrationProcessorSideBase, new()
        {
            return TypeCollection<TProcessor>.GetOrAdd(logger, () =>
            {
                var side = (TIntegrationProcessorSide)System.Activator.CreateInstance(typeof(TIntegrationProcessorSide));
                var processor = (TProcessor)System.Activator.CreateInstance(typeof(TProcessor));
                processor.IntegrationProcessorSide = side;
                processor.Configuration = configuration;
                processor.Logger = logger;
                processor.Publisher = publisher;

                side.Processor = processor;

                return processor;
            });
        }

        /// <summary>
        /// Обработать входное сообщение, запрос и т.п.
        /// </summary>
        /// <param name="message">Входное сообщение, запрос и т.п.</param>
        protected internal virtual void Process(object message)
        {
            var inboxMessage = new InboxMessage(message);
            var validator = IntegrationProcessorSide.GetValidator(Publisher, Configuration, Logger);
            if (validator != null)
            {
                validator.Validate(inboxMessage);
            }

            var logging = IntegrationProcessorSide.GetLogging(Publisher, Configuration, Logger);
            if (logging != null)
            {
                logging.LogInboxMessage(inboxMessage);
            }

            if (inboxMessage.IsFailed)
            {
                var failedProcessing = IntegrationProcessorSide.GetInboxMessageFailedProcessing(Publisher, Configuration, Logger);
                if (failedProcessing != null)
                {
                    failedProcessing.ProcessFailedInboxMessage(inboxMessage);
                    return;
                }
                throw new NotImplementedException("Отсутствует обработка результата не прошедшего проверку.");
            }

            var formatterInboxMessage = IntegrationProcessorSide.GetFormatterInboxMessage(Publisher, Configuration, Logger);
            if (formatterInboxMessage != null)
            {
                inboxMessage = formatterInboxMessage.FormatInboxMessage(inboxMessage);
            }

            var inboxMessageProcessing = IntegrationProcessorSide.GetInboxMessageProcessing(Publisher, Configuration, Logger);
            if (inboxMessageProcessing == null)
            {
                throw new NotImplementedException("Отсутствует обработка результата.");
            }

            inboxMessageProcessing.ProcessInboxMessage(inboxMessage);
        }
    }
}
