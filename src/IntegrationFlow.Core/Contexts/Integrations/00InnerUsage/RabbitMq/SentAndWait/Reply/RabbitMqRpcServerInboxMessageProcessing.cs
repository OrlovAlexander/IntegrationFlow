using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;

/// <summary>
/// Inbox handler for hosted RPC server: reply is published before consumer ack.
/// </summary>
public sealed class RabbitMqRpcServerInboxMessageProcessing : IInboxMessageProcessing
{
    private readonly RabbitMqReplyPublisher replyPublisher;
    private readonly Func<RabbitMqReceivedMessage, Task<string>> buildResponseAsync;
    private readonly IRequestReplyResponseStore? responseStore;

    /// <summary>
    /// Creates RPC server inbox handler from request-reply profile name.
    /// </summary>
    public RabbitMqRpcServerInboxMessageProcessing(
        string requestReplyProfileName,
        Func<RabbitMqReceivedMessage, Task<string>>? buildResponseAsync = null,
        IRequestReplyResponseStore? responseStore = null)
        : this(
            LoadConfiguration(requestReplyProfileName),
            buildResponseAsync,
            responseStore)
    {
    }

    /// <summary>
    /// Creates RPC server inbox handler from loaded configuration.
    /// </summary>
    public RabbitMqRpcServerInboxMessageProcessing(
        RabbitMqRequestReplyConfiguration configuration,
        Func<RabbitMqReceivedMessage, Task<string>>? buildResponseAsync = null,
        IRequestReplyResponseStore? responseStore = null)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration.Validate();
        replyPublisher = new RabbitMqReplyPublisher(configuration);
        this.buildResponseAsync = buildResponseAsync ?? DefaultBuildResponseAsync;
        this.responseStore = responseStore;
    }

    /// <inheritdoc />
    public void ProcessInboxMessage(InboxMessage inboxMessage)
    {
        if (inboxMessage?.Message is not RabbitMqReceivedMessage receivedMessage || !receivedMessage.IsRequestReply)
        {
            return;
        }

        RabbitMqRpcServerPipeline
            .HandleAsync(receivedMessage, replyPublisher, buildResponseAsync, responseStore, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static RabbitMqRequestReplyConfiguration LoadConfiguration(string requestReplyProfileName)
    {
        if (string.IsNullOrWhiteSpace(requestReplyProfileName))
        {
            throw new ArgumentException("Request-reply profile name is required.", nameof(requestReplyProfileName));
        }

        return RabbitMqRequestReplyConfigurationLoader.LoadProfile(requestReplyProfileName);
    }

    private static Task<string> DefaultBuildResponseAsync(RabbitMqReceivedMessage request)
        => Task.FromResult($$"""{"status":"ok","echo":{{request.BodyText}}}""");
}
