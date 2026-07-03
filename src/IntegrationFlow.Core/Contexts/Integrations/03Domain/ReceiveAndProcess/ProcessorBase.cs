using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;

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
        protected IntegrationProcessorSideBase IntegrationProcessorSide { get; private set; }

        /// <summary>
        /// Ctor
        /// </summary>
        protected ProcessorBase()
        {
        }

        /// <summary>
        /// Создать обработчик входящего сообщения, запроса и т.п.
        /// </summary>
        public static ProcessorBase Create<TProcessor, TIntegrationProcessorSide>(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger,
            string cacheKeySuffix = null)
            where TProcessor : ProcessorBase, new()
            where TIntegrationProcessorSide : IntegrationProcessorSideBase, new()
        {
            var sideKey = typeof(TIntegrationProcessorSide).AssemblyQualifiedName
                ?? typeof(TIntegrationProcessorSide).FullName
                ?? typeof(TIntegrationProcessorSide).Name;
            var cacheKey = string.IsNullOrWhiteSpace(cacheKeySuffix)
                ? $"{typeof(TProcessor).AssemblyQualifiedName}|{sideKey}"
                : $"{typeof(TProcessor).AssemblyQualifiedName}|{sideKey}|{cacheKeySuffix}";

            return TypeCollection<TProcessor>.GetOrAdd(logger, cacheKey, () =>
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
        /// Creates processor wired to an explicit processor side instance (hosted listener).
        /// </summary>
        internal static TProcessor CreateWithProcessorSide<TProcessor>(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger,
            IntegrationProcessorSideBase processorSide,
            string cacheKeySuffix)
            where TProcessor : ProcessorBase, new()
        {
            if (processorSide == null)
            {
                throw new ArgumentNullException(nameof(processorSide));
            }

            var cacheKey = string.IsNullOrWhiteSpace(cacheKeySuffix)
                ? $"{typeof(TProcessor).AssemblyQualifiedName}|hosted|{processorSide.GetType().FullName}"
                : $"{typeof(TProcessor).AssemblyQualifiedName}|hosted|{processorSide.GetType().FullName}|{cacheKeySuffix}";

            return TypeCollection<TProcessor>.GetOrAdd(logger, cacheKey, () =>
            {
                var processor = new TProcessor();
                processor.IntegrationProcessorSide = processorSide;
                processor.Configuration = configuration;
                processor.Logger = logger;
                processor.Publisher = publisher;
                processorSide.Processor = processor;
                return processor;
            });
        }

        /// <summary>
        /// Обработать входное сообщение для listener (internal entry point).
        /// </summary>
        internal Task ProcessMessageAsync(object message, CancellationToken cancellationToken = default)
            => ProcessAsync(message, cancellationToken);

        /// <summary>
        /// Обработать входное сообщение, запрос и т.п.
        /// </summary>
        protected internal virtual void Process(object message)
        {
            ProcessAsync(message, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Асинхронно обработать входное сообщение, запрос и т.п.
        /// </summary>
        protected internal virtual async Task ProcessAsync(object message, CancellationToken cancellationToken = default)
        {
            var deduplicationStore = IntegrationProcessorSide.GetMessageDeduplicationStore(Publisher, Configuration, Logger);
            var messageId = ExtractMessageId(message);
            var processingAcquired = false;

            if (deduplicationStore != null && !string.IsNullOrWhiteSpace(messageId))
            {
                var beginResult = await deduplicationStore
                    .TryBeginProcessingAsync(messageId, cancellationToken)
                    .ConfigureAwait(false);

                switch (beginResult)
                {
                    case DeduplicationBeginResult.AlreadyProcessed:
                        Logger.LogInfo(SR.T("Сообщение '{0}' уже обработано, пропуск.", messageId));
                        return;
                    case DeduplicationBeginResult.InProgress:
                        throw new MessageProcessingInProgressException(messageId);
                    case DeduplicationBeginResult.Acquired:
                        processingAcquired = true;
                        break;
                }
            }
            else if (deduplicationStore != null && string.IsNullOrWhiteSpace(messageId))
            {
                Logger.LogWarn(SR.T(
                    "Dedup store настроен, но MessageId отсутствует — идемпотентность пропущена."));
            }

            try
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
                    throw new NotImplementedException(SR.T("Отсутствует обработка результата не прошедшего проверку."));
                }

                var formatterInboxMessage = IntegrationProcessorSide.GetFormatterInboxMessage(Publisher, Configuration, Logger);
                if (formatterInboxMessage != null)
                {
                    inboxMessage = formatterInboxMessage.FormatInboxMessage(inboxMessage);
                }

                var inboxMessageProcessing = IntegrationProcessorSide.GetInboxMessageProcessing(Publisher, Configuration, Logger);
                if (inboxMessageProcessing == null)
                {
                    throw new NotImplementedException(SR.T("Отсутствует обработка результата."));
                }

                inboxMessageProcessing.ProcessInboxMessage(inboxMessage);

                if (deduplicationStore != null && !string.IsNullOrWhiteSpace(messageId))
                {
                    await deduplicationStore
                        .MarkProcessedAsync(messageId, cancellationToken)
                        .ConfigureAwait(false);
                    processingAcquired = false;
                }
            }
            finally
            {
                if (processingAcquired &&
                    deduplicationStore != null &&
                    !string.IsNullOrWhiteSpace(messageId))
                {
                    await deduplicationStore
                        .ReleaseProcessingAsync(messageId, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        private static string ExtractMessageId(object message)
        {
            return message is IIntegrationMessageMetadata metadata
                ? metadata.MessageId
                : string.Empty;
        }
    }
}
