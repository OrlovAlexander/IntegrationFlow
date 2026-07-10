using System;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;

/// <summary>
/// HTTP transport failure for REST SentAndWait.
/// </summary>
public sealed class RestHttpException : Exception
{
    public RestHttpException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}
