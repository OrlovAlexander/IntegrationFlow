using System;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;

/// <summary>
/// Non-transient HTTP client error (4xx) for REST publish.
/// </summary>
public sealed class RestHttpClientErrorException : Exception, INonRetryableOutboxPublishException
{
    public RestHttpClientErrorException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
