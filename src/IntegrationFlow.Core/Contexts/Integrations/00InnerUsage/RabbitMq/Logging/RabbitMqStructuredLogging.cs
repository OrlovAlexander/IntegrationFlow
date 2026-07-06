using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;

internal static class RabbitMqStructuredLogging
{
    public static IDisposable BeginMessageScope(
        IIntegrationLogger logger,
        string profile,
        RabbitMqReceivedMessage message,
        RabbitMqTransportLogKind kind)
        => IntegrationStructuredLogging.BeginScope(
            logger,
            (IntegrationStructuredLogFields.Profile, profile),
            (IntegrationStructuredLogFields.MessageId, message.MessageId),
            (IntegrationStructuredLogFields.CorrelationId, message.CorrelationId),
            (IntegrationStructuredLogFields.DeliveryTag, message.DeliveryTag),
            (IntegrationStructuredLogFields.Kind, ToKindString(kind)));

    public static IDisposable BeginTransportScope(
        IIntegrationLogger logger,
        string profile,
        RabbitMqTransportLogKind kind)
        => IntegrationStructuredLogging.BeginScope(
            logger,
            (IntegrationStructuredLogFields.Profile, profile),
            (IntegrationStructuredLogFields.Kind, ToKindString(kind)));

    public static IDisposable BeginPublishScope(
        IIntegrationLogger logger,
        string profile,
        string messageId,
        string? correlationId,
        RabbitMqTransportLogKind kind)
        => IntegrationStructuredLogging.BeginScope(
            logger,
            (IntegrationStructuredLogFields.Profile, profile),
            (IntegrationStructuredLogFields.MessageId, messageId),
            (IntegrationStructuredLogFields.CorrelationId, correlationId),
            (IntegrationStructuredLogFields.Kind, ToKindString(kind)));

    internal static string ToKindString(RabbitMqTransportLogKind kind)
        => kind switch
        {
            RabbitMqTransportLogKind.Listener => "listener",
            RabbitMqTransportLogKind.OutboxRelay => "outbox_relay",
            RabbitMqTransportLogKind.RpcCorrelation => "rpc_correlation",
            RabbitMqTransportLogKind.Publish => "publish",
            RabbitMqTransportLogKind.RequestReply => "request_reply",
            _ => kind.ToString(),
        };
}
