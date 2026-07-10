using System;
using System.Collections.Generic;
using System.Text;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;

/// <summary>
/// Inbound webhook payload received through an ASP.NET endpoint.
/// </summary>
public sealed class RestWebhookReceivedMessage : IIntegrationMessageMetadata
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// REST webhook profile name from rest.json.
    /// </summary>
    public string ProfileName { get; }

    /// <summary>
    /// Raw request body.
    /// </summary>
    public byte[] Body { get; }

    /// <summary>
    /// Request body as UTF-8 text.
    /// </summary>
    public string BodyText => Encoding.UTF8.GetString(Body);

    /// <summary>
    /// Message id from configured header (for deduplication).
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Correlation id from configured header or trace context.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Request Content-Type header value.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// HTTP request path that received the webhook.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Snapshot of request headers (case-insensitive keys).
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// UTC timestamp when the request was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; }

    internal RestWebhookReceivedMessage(
        string profileName,
        byte[] body,
        string messageId,
        string correlationId,
        string contentType,
        string path,
        IReadOnlyDictionary<string, string>? headers,
        DateTimeOffset receivedAt)
    {
        ProfileName = profileName ?? string.Empty;
        Body = body ?? Array.Empty<byte>();
        MessageId = messageId ?? string.Empty;
        CorrelationId = correlationId ?? string.Empty;
        ContentType = contentType ?? string.Empty;
        Path = path ?? string.Empty;
        Headers = headers == null || headers.Count == 0 ? EmptyHeaders : headers;
        ReceivedAt = receivedAt;
    }
}
