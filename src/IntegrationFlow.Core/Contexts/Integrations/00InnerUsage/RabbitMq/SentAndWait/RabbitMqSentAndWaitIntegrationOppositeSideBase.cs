using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Validator;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait
{
    /// <summary>
    /// Базовая противоположная сторона SentAndWait для request-reply через RabbitMQ.
    /// </summary>
    internal abstract class RabbitMqSentAndWaitIntegrationOppositeSideBase : SentAndWaitIntegrationOppositeSide
    {
        /// <summary>
        /// Имя профиля в rabbitmq.json (секция RabbitMqRequestReply).
        /// </summary>
        protected abstract string ConfigurationName { get; }

        /// <inheritdoc />
        public override IFormatterTransmitData GetFormatterSourceData(IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override IConfiguration GetTransmitterConfiguration(IIntegrationLogger logger)
            => RabbitMqRequestReplyConfigurationLoader.LoadProfile(ConfigurationName);

        /// <inheritdoc />
        public override IConnection GetConnection(IConfiguration configuration, IIntegrationLogger logger)
        {
            var requestReplyConfiguration = (RabbitMqRequestReplyConfiguration)configuration;
            return requestReplyConfiguration.ReuseConnection
                ? RabbitMqRequestReplyConnectionPool.GetOrAdd(requestReplyConfiguration)
                : new RabbitMqRequestReplyConnection(requestReplyConfiguration);
        }

        /// <inheritdoc />
        public override ITransmitter GetTransmitter(IConfiguration configuration, IConnection connection, IIntegrationLogger logger)
            => new RabbitMqRequestReplyTransmitter(
                configuration,
                (RabbitMqRequestReplyConnection)connection);

        /// <inheritdoc />
        public override IValidator GetValidator(IConfiguration configuration, IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override IFormatterObtainedData GetFormatterObtainedData(IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override ILogging GetLogging(IIntegrationLogger logger) => null;
    }

    /// <summary>
    /// Противоположная сторона SentAndWait с динамическим именем профиля конфигурации.
    /// </summary>
    internal sealed class NamedRabbitMqSentAndWaitIntegrationOppositeSide : RabbitMqSentAndWaitIntegrationOppositeSideBase
    {
        private readonly string configurationName;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="configurationName">Имя профиля в rabbitmq.json.</param>
        public NamedRabbitMqSentAndWaitIntegrationOppositeSide(string configurationName)
        {
            if (string.IsNullOrWhiteSpace(configurationName))
            {
                throw new ArgumentException("Имя профиля RabbitMQ request-reply не задано.", nameof(configurationName));
            }

            this.configurationName = configurationName;
        }

        /// <inheritdoc />
        protected override string ConfigurationName => configurationName;

        /// <inheritdoc />
        protected override object GetIntegrationOppositeSideCode() => configurationName;
    }
}
