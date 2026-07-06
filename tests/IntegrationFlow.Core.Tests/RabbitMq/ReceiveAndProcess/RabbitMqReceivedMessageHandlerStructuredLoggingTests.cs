using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.ReceiveAndProcess;

public sealed class RabbitMqReceivedMessageHandlerStructuredLoggingTests
{
    [Fact]
    public async Task HandleAsync_Ack_AddsStructuredScopeFields()
    {
        var capture = new ScopeCapturingLogger();
        var logger = new MicrosoftExtensionsIntegrationLogger(capture);
        var acknowledgement = new RecordingAcknowledgement();
        var handler = new RabbitMqReceivedMessageHandler(
            _ => Task.CompletedTask,
            acknowledgement,
            logger,
            profileName: "Inbox");

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 42),
            new RabbitMqConfiguration(),
            CancellationToken.None);

        Assert.Contains(capture.Scopes, scope => ContainsField(scope, IntegrationStructuredLogFields.Profile, "Inbox"));
        Assert.Contains(capture.Scopes, scope => ContainsField(scope, IntegrationStructuredLogFields.MessageId, "msg-id"));
        Assert.Contains(capture.Scopes, scope => ContainsField(scope, IntegrationStructuredLogFields.CorrelationId, "corr-id"));
        Assert.Contains(capture.Scopes, scope => ContainsField(scope, IntegrationStructuredLogFields.DeliveryTag, 42UL));
        Assert.Contains(capture.Scopes, scope => ContainsField(scope, IntegrationStructuredLogFields.Kind, "listener"));
        Assert.Contains(capture.Scopes, scope => ContainsField(scope, IntegrationStructuredLogFields.Outcome, "consume_started"));
        Assert.Contains(capture.Scopes, scope => ContainsField(scope, IntegrationStructuredLogFields.Outcome, "ack"));
    }

    [Fact]
    public async Task HandleAsync_InProgressDedup_AddsInProgressRequeueOutcome()
    {
        var capture = new ScopeCapturingLogger();
        var logger = new MicrosoftExtensionsIntegrationLogger(capture);
        var acknowledgement = new RecordingAcknowledgement();
        var handler = new RabbitMqReceivedMessageHandler(
            _ => Task.FromException(new MessageProcessingInProgressException("msg-1")),
            acknowledgement,
            logger,
            profileName: "Inbox");

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 9),
            new RabbitMqConfiguration(),
            CancellationToken.None);

        Assert.Contains(
            capture.Scopes,
            scope => ContainsField(scope, IntegrationStructuredLogFields.Outcome, "in_progress_requeue"));
    }

    private static bool ContainsField(object scopeState, string key, object expected)
    {
        if (scopeState is not Dictionary<string, object?> scope)
        {
            return false;
        }

        return scope.TryGetValue(key, out var value) && Equals(value, expected);
    }

    private static RabbitMqReceivedMessage CreateMessage(ulong deliveryTag)
        => new(new byte[] { 1 }, deliveryTag, "rk", "msg-id", "corr-id");

    private sealed class ScopeCapturingLogger : ILogger
    {
        public List<object> Scopes { get; } = new();

        public IDisposable BeginScope<TState>(TState state)
        {
            Scopes.Add(state!);
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class RecordingAcknowledgement : IRabbitMqMessageAcknowledgement
    {
        public void Acknowledge(ulong deliveryTag)
        {
        }

        public void NegativeAcknowledge(ulong deliveryTag, bool requeue)
        {
        }
    }
}
