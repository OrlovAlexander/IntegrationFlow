using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Validator;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess
{
    /// <summary>
    /// Базовая сторона обработчика входящих сообщений RabbitMQ.
    /// </summary>
    internal sealed class DefaultRabbitMqIntegrationProcessorSide : IntegrationProcessorSideBase
    {
        /// <inheritdoc />
        public override IValidator GetValidator(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override ILogging GetLogging(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override IInboxMessageFailedProcessing GetInboxMessageFailedProcessing(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override IFormatterInboxMessage GetFormatterInboxMessage(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override IInboxMessageProcessing GetInboxMessageProcessing(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
            => NoOpInboxMessageProcessing.Instance;
    }

    /// <summary>
    /// Базовая сторона публикатора для интеграции прослушивания очереди RabbitMQ.
    /// </summary>
    internal abstract class RabbitMqIntegrationPublisherSideBase : IntegrationPublisherSideBase
    {
        /// <inheritdoc />
        public override ListenerBase GetListener(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
            => ListenerBase.Create<RabbitMqListener>(publisher, configuration, logger);

        /// <inheritdoc />
        public override ProcessorBase GetProcessor(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
            => ProcessorBase.Create<RabbitMqProcessor, DefaultRabbitMqIntegrationProcessorSide>(publisher, configuration, logger);
    }
}
