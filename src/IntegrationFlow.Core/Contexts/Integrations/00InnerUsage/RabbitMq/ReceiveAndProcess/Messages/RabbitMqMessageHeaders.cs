using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;

/// <summary>
/// Утилиты для чтения AMQP headers из <see cref="RabbitMqReceivedMessage"/>.
/// </summary>
public static class RabbitMqMessageHeaders
{
    /// <summary>
    /// W3C <c>traceparent</c> header (distributed tracing).
    /// </summary>
    public const string TraceParent = "traceparent";

    /// <summary>
    /// W3C <c>tracestate</c> header (distributed tracing).
    /// </summary>
    public const string TraceState = "tracestate";

    /// <summary>
    /// RabbitMQ dead-letter metadata header.
    /// </summary>
    public const string Death = "x-death";

    private static readonly IReadOnlyDictionary<string, object> EmptyHeaders =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Пустой набор headers (immutable).
    /// </summary>
    public static IReadOnlyDictionary<string, object> Empty => EmptyHeaders;

    /// <summary>
    /// Создаёт read-only снимок AMQP headers.
    /// </summary>
    public static IReadOnlyDictionary<string, object> Snapshot(IDictionary<string, object>? headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return Empty;
        }

        return new Dictionary<string, object>(headers, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Возвращает строковое значение header (поддерживает <c>string</c> и <c>byte[]</c>).
    /// </summary>
    public static bool TryGetString(IReadOnlyDictionary<string, object> headers, string key, out string value)
    {
        value = string.Empty;
        if (headers == null || headers.Count == 0 || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!headers.TryGetValue(key, out var raw))
        {
            foreach (var entry in headers)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    raw = entry.Value;
                    break;
                }
            }
        }

        if (raw == null)
        {
            return false;
        }

        value = raw switch
        {
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
#if NET8_0_OR_GREATER
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            Memory<byte> memory => Encoding.UTF8.GetString(memory.Span),
#endif
            _ => raw.ToString() ?? string.Empty,
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Возвращает количество смертей сообщения из <c>x-death</c> (для DLQ / retry policy).
    /// </summary>
    public static int GetDeathCount(IReadOnlyDictionary<string, object> headers)
    {
        if (headers == null || !headers.TryGetValue(Death, out var deathHeader))
        {
            return 0;
        }

        if (deathHeader is IList deathList)
        {
            var total = 0;
            foreach (var entry in deathList)
            {
                if (entry is IDictionary deathEntry && deathEntry.Contains("count"))
                {
                    total += Convert.ToInt32(Convert.ToInt64(deathEntry["count"]!));
                }
            }

            return total;
        }

        return 0;
    }
}
