using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot
{
    /// <summary>
    /// Базовая противоположная сторона SentAndForgot для публикации в RabbitMQ.
    /// </summary>
    internal abstract class RabbitMqSentAndForgotIntegrationOppositeSideBase : SentAndForgotIntegrationOppositeSide
    {
        /// <summary>
        /// Имя профиля в rabbitmq.json (секция RabbitMqPublish).
        /// </summary>
        protected abstract string ConfigurationName { get; }

        /// <inheritdoc />
        public override IFormatterTransmitData GetFormatterSourceData(IIntegrationLogger logger) => null;

        /// <inheritdoc />
        public override IConfiguration GetTransmitterConfiguration(IIntegrationLogger logger)
            => RabbitMqPublishConfigurationLoader.LoadProfile(ConfigurationName);

        /// <inheritdoc />
        public override IConnection GetConnection(IConfiguration configuration, IIntegrationLogger logger)
            => new RabbitMqPublishConnection((RabbitMqPublishConfiguration)configuration);

        /// <inheritdoc />
        public override ITransmitter GetTransmitter(IConfiguration configuration, IConnection connection, IIntegrationLogger logger)
            => new RabbitMqPublishTransmitter(
                (RabbitMqPublishConfiguration)configuration,
                (RabbitMqPublishConnection)connection);

        /// <inheritdoc />
        public override ILogging GetLogging(IIntegrationLogger logger) => null;
    }

    /// <summary>
    /// Противоположная сторона SentAndForgot с динамическим именем профиля конфигурации.
    /// </summary>
    internal sealed class NamedRabbitMqSentAndForgotIntegrationOppositeSide : RabbitMqSentAndForgotIntegrationOppositeSideBase
    {
        private readonly string configurationName;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="configurationName">Имя профиля в rabbitmq.json.</param>
        public NamedRabbitMqSentAndForgotIntegrationOppositeSide(string configurationName)
        {
            if (string.IsNullOrWhiteSpace(configurationName))
            {
                throw new ArgumentException("Имя профиля RabbitMQ publish не задано.", nameof(configurationName));
            }

            this.configurationName = configurationName;
        }

        /// <inheritdoc />
        protected override string ConfigurationName => configurationName;

        /// <inheritdoc />
        protected override object GetIntegrationOppositeSideCode() => configurationName;
    }
}
