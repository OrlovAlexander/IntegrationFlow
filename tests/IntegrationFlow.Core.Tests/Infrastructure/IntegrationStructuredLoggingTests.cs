using System;
using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IntegrationFlow.Tests.Infrastructure;

public sealed class IntegrationStructuredLoggingTests
{
    [Fact]
    public void BeginScope_AddsStructuredFieldsToLoggerScope()
    {
        var capture = new ScopeCapturingLogger();
        var logger = new MicrosoftExtensionsIntegrationLogger(capture);

        using (IntegrationStructuredLogging.BeginScope(
                   logger,
                   (IntegrationStructuredLogFields.Profile, "Inbox"),
                   (IntegrationStructuredLogFields.MessageId, "msg-1"),
                   (IntegrationStructuredLogFields.CorrelationId, "corr-1"),
                   (IntegrationStructuredLogFields.DeliveryTag, 42UL),
                   (IntegrationStructuredLogFields.Kind, "listener"),
                   (IntegrationStructuredLogFields.Outcome, "ack")))
        {
            logger.Log("test");
        }

        Assert.Single(capture.Scopes);
        var scope = Assert.IsType<Dictionary<string, object?>>(capture.Scopes[0]);
        Assert.Equal("Inbox", scope[IntegrationStructuredLogFields.Profile]);
        Assert.Equal("msg-1", scope[IntegrationStructuredLogFields.MessageId]);
        Assert.Equal("corr-1", scope[IntegrationStructuredLogFields.CorrelationId]);
        Assert.Equal(42UL, scope[IntegrationStructuredLogFields.DeliveryTag]);
        Assert.Equal("listener", scope[IntegrationStructuredLogFields.Kind]);
        Assert.Equal("ack", scope[IntegrationStructuredLogFields.Outcome]);
    }

    [Fact]
    public void BeginScope_WithNullIntegrationLogger_ReturnsNoOpScope()
    {
        var scope = IntegrationStructuredLogging.BeginScope(
            NullIntegrationLogger.Instance,
            (IntegrationStructuredLogFields.Profile, "Inbox"));

        Assert.NotNull(scope);
        scope.Dispose();
    }

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
}
