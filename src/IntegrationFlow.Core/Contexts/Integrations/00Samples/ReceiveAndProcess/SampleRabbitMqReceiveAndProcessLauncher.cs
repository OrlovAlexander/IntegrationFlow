using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.ExtensionsPoints;

namespace IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess
{
    /// <summary>
    /// Пример конфигурации подключения к RabbitMQ для организации.
    /// </summary>
    public sealed class SampleRabbitMqConfiguration : RabbitMqConfiguration
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public SampleRabbitMqConfiguration()
        {
            HostName = "localhost";
            Port = 5672;
            UserName = "guest";
            Password = "guest";
            VirtualHost = "/";
            QueueName = "integration.inbox";
            PrefetchCount = 1;
            Asynchronously = true;
        }
    }

    /// <summary>
    /// Пример стороны публикатора для организации.
    /// </summary>
    internal sealed class SampleRabbitMqIntegrationPublisherSide : RabbitMqIntegrationPublisherSideBase
    {
        /// <inheritdoc />
        public override IConfiguration GetConfiguration(PublisherBase publisher, IIntegrationLogger logger)
            => new SampleRabbitMqConfiguration();
    }

    /// <summary>
    /// Пример запуска интеграции "Получить и обработать" для RabbitMQ.
    /// </summary>
    public sealed class SampleRabbitMqReceiveAndProcessLauncher : IReceiveAndProcessLauncher
    {
        /// <inheritdoc />
        public void Run()
        {
            var publisher = PublisherBase.Create<RabbitMqPublisher, SampleRabbitMqIntegrationPublisherSide>(Logger.Create());
            publisher.BeginReceiving();
        }
    }
}
