using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.ExtensionsPoints;

namespace IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess
{
    /// <summary>
    /// Пример конфигурации подключения к RabbitMQ для организации.
    /// Значения загружаются из профиля <c>Inbox</c> файла <c>rabbitmq.json</c>.
    /// </summary>
    public sealed class SampleRabbitMqConfiguration : RabbitMqConfiguration
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public SampleRabbitMqConfiguration()
        {
            RabbitMqConfigurationLoader.PopulateProfile(this, "Inbox");
        }
    }

    /// <summary>
    /// Пример стороны публикатора для профиля Inbox.
    /// </summary>
    internal sealed class InboxRabbitMqPublisherSide : RabbitMqIntegrationPublisherSideBase
    {
        /// <inheritdoc />
        protected override string ConfigurationName => "Inbox";
    }

    /// <summary>
    /// Пример стороны публикатора для профиля Orders.
    /// </summary>
    internal sealed class OrdersRabbitMqPublisherSide : RabbitMqIntegrationPublisherSideBase
    {
        /// <inheritdoc />
        protected override string ConfigurationName => "Orders";
    }

    /// <summary>
    /// Пример запуска интеграции "Получить и обработать" для профиля Inbox.
    /// </summary>
    public sealed class SampleRabbitMqReceiveAndProcessLauncher : IReceiveAndProcessLauncher
    {
        /// <inheritdoc />
        public void Run()
        {
            var publisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(Logger.Create());
            publisher.BeginReceiving();
        }
    }

    /// <summary>
    /// Пример запуска интеграции "Получить и обработать" для профиля Orders.
    /// </summary>
    public sealed class SampleRabbitMqOrdersReceiveAndProcessLauncher : IReceiveAndProcessLauncher
    {
        /// <inheritdoc />
        public void Run()
        {
            var publisher = PublisherBase.Create<RabbitMqPublisher, OrdersRabbitMqPublisherSide>(Logger.Create());
            publisher.BeginReceiving();
        }
    }

    /// <summary>
    /// Пример запуска интеграции "Получить и обработать" для всех профилей RabbitMQ.
    /// </summary>
    public sealed class SampleRabbitMqAllProfilesReceiveAndProcessLauncher : IReceiveAndProcessLauncher
    {
        /// <inheritdoc />
        public void Run()
        {
            foreach (var configuration in RabbitMqConfigurationLoader.LoadAll())
            {
                var side = new NamedRabbitMqIntegrationPublisherSide(configuration.Name);
                var publisher = PublisherBase.Create<RabbitMqPublisher>(Logger.Create(), side);
                publisher.BeginReceiving();
            }
        }
    }
}
