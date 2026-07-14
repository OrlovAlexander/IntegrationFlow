using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace Bridge.Worker.Application;

public sealed class ForwardPayloadHandler
{
    private const string RestProfileName = "StorageIngest";

    private readonly IIntegrationLogger integrationLogger;
    private readonly ILogger<ForwardPayloadHandler> logger;

    public ForwardPayloadHandler(
        IIntegrationLogger integrationLogger,
        ILogger<ForwardPayloadHandler> logger)
    {
        this.integrationLogger = integrationLogger;
        this.logger = logger;
    }

    public Task HandleAsync(InboxMessage inboxMessage, CancellationToken cancellationToken)
    {
        if (inboxMessage.Message is not RabbitMqReceivedMessage receivedMessage)
        {
            throw new InvalidOperationException("Expected RabbitMqReceivedMessage in inbox pipeline.");
        }

        var messageId = string.IsNullOrWhiteSpace(receivedMessage.MessageId)
            ? Guid.NewGuid().ToString("N")
            : receivedMessage.MessageId;
        var correlationId = string.IsNullOrWhiteSpace(receivedMessage.CorrelationId)
            ? messageId
            : receivedMessage.CorrelationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            [IntegrationStructuredLogFields.MessageId] = messageId,
            [IntegrationStructuredLogFields.CorrelationId] = correlationId,
        }))
        {
            logger.LogInformation("PayloadReceived DeliveryTag={DeliveryTag}", receivedMessage.DeliveryTag);

            var transmitData = new TransmitData(receivedMessage.BodyText, messageId, correlationId);
            var oppositeSide = new NamedRestSentAndForgotIntegrationOppositeSide(RestProfileName);
            var result = SentAndForgotTransmitter.Execute(oppositeSide, transmitData, integrationLogger);

            logger.LogInformation(
                "PayloadForwarded RestProfile={RestProfile} PublishedMessageId={PublishedMessageId}",
                RestProfileName,
                result.MessageId);
        }

        return Task.CompletedTask;
    }
}

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
