using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;
using RmqSAF_RmqRAP_Rest.Contracts;
using Sender.Api.Domain;

namespace Sender.Api.Infrastructure.IntegrationFlow;

internal static class SentAndForgotTransmitter
{
    public static TransmitResult Execute(
        SentAndForgotIntegrationOppositeSide oppositeSide,
        TransmitData transmitData,
        IIntegrationLogger logger)
    {
        var configuration = oppositeSide.GetTransmitterConfiguration(logger);
        using var connection = oppositeSide.GetConnection(configuration, logger)
            ?? throw new InvalidOperationException("Integration connection was not provided.");

        if (connection.NeedReconnect() && !connection.Reconnect())
        {
            throw new InvalidOperationException("Integration reconnect failed.");
        }

        var transmitter = oppositeSide.GetTransmitter(configuration, connection, logger)
            ?? throw new InvalidOperationException("Integration transmitter was not provided.");

        if (transmitter is ITransmitterWithResult transmitterWithResult)
        {
            return transmitterWithResult.TransmitWithResult(transmitData);
        }

        transmitter.Transmit(transmitData);
        return TransmitResult.Create(transmitData.MessageId);
    }
}

internal sealed class RabbitMqSentAndForgotGateway : IIntegrationPublisher
{
    private const string PublishProfileName = "E2EOut";

    private readonly IIntegrationLogger integrationLogger;
    private readonly ILogger<RabbitMqSentAndForgotGateway> logger;

    public RabbitMqSentAndForgotGateway(
        IIntegrationLogger integrationLogger,
        ILogger<RabbitMqSentAndForgotGateway> logger)
    {
        this.integrationLogger = integrationLogger;
        this.logger = logger;
    }

    public PublishResult Publish(PayloadEnvelope payload, CancellationToken cancellationToken)
    {
        var transmitData = new TransmitData(payload, payload.MessageId, payload.CorrelationIdText);

        using (logger.BeginScope(new Dictionary<string, object>
        {
            [IntegrationStructuredLogFields.MessageId] = payload.MessageId,
            [IntegrationStructuredLogFields.CorrelationId] = payload.CorrelationIdText,
        }))
        {
            logger.LogInformation("PayloadAccepted Type={PayloadType}", payload.Type);

            var oppositeSide = new NamedRabbitMqSentAndForgotIntegrationOppositeSide(PublishProfileName);
            var result = SentAndForgotTransmitter.Execute(oppositeSide, transmitData, integrationLogger);

            logger.LogInformation("PayloadPublished QueueProfile={Profile}", PublishProfileName);
            return new PublishResult(payload.MessageId, payload.CorrelationIdText, true, null);
        }
    }
}
