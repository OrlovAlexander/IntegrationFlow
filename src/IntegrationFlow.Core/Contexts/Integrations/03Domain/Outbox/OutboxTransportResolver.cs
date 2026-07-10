using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

/// <summary>
/// Resolves outbox relay transport from REST publish or RabbitMQ publish configuration.
/// </summary>
public sealed class OutboxTransportResolver : IOutboxTransportResolver
{
    public IOutboxRelayPublisher CreatePublisher(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Outbox profile name is required.", nameof(profileName));
        }

        if (RestPublishConfigurationLoader.TryLoadProfile(profileName, out var restConfiguration))
        {
            return new RestOutboxRelayPublisher(restConfiguration);
        }

        try
        {
            var rabbitConfiguration = RabbitMqPublishConfigurationLoader.LoadProfile(profileName);
            return new RabbitMqOutboxRelayPublisher(profileName, rabbitConfiguration);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Outbox profile '{profileName}' was not found in RestPublish or RabbitMqPublish configuration.",
                ex);
        }
    }

    private sealed class RestOutboxRelayPublisher : IOutboxRelayPublisher
    {
        private readonly RestPublishConnection connection;
        private readonly RestPublishTransmitter transmitter;

        public RestOutboxRelayPublisher(RestPublishConfiguration configuration)
        {
            connection = new RestPublishConnection(configuration);
            transmitter = new RestPublishTransmitter(configuration, connection);
        }

        public OutboxTransportKind TransportKind => OutboxTransportKind.Rest;

        public ITransmitterWithResult Transmitter => transmitter;

        public void Dispose()
        {
            connection.Dispose();
        }
    }

    private sealed class RabbitMqOutboxRelayPublisher : IOutboxRelayPublisher
    {
        private readonly string profileName;
        private RabbitMqPublishConfiguration? configuration;
        private RabbitMqPublishConnection? connection;
        private RabbitMqPublishTransmitter? transmitter;

        public RabbitMqOutboxRelayPublisher(string profileName, RabbitMqPublishConfiguration configuration)
        {
            this.profileName = profileName;
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public OutboxTransportKind TransportKind => OutboxTransportKind.RabbitMq;

        public ITransmitterWithResult Transmitter => GetOrCreateTransmitter();

        public void Dispose()
        {
            connection?.Dispose();
        }

        private ITransmitterWithResult GetOrCreateTransmitter()
        {
            if (connection == null || connection.NeedReconnect())
            {
                connection?.Dispose();
                configuration ??= RabbitMqPublishConfigurationLoader.LoadProfile(profileName);
                connection = new RabbitMqPublishConnection(configuration);
                transmitter = new RabbitMqPublishTransmitter(configuration, connection);
            }

            return transmitter!;
        }
    }
}
