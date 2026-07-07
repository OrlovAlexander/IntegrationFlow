using System;
using System.Globalization;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

/// <summary>
/// Maps publish/RPC configuration to AMQP <see cref="IBasicProperties"/>.
/// </summary>
internal static class RabbitMqBasicPropertiesMapper
{
    internal static void ApplyDeliveryProperties(
        IBasicProperties properties,
        string contentType,
        bool persistent,
        byte? priority,
        int? expirationMilliseconds)
    {
        if (properties == null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        properties.ContentType = contentType;
        properties.DeliveryMode = persistent ? (byte)2 : (byte)1;

        if (priority.HasValue)
        {
            properties.Priority = priority.Value;
        }

        if (expirationMilliseconds.HasValue)
        {
            properties.Expiration = expirationMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal static void ValidateMessageDeliveryOptions(int? expirationMilliseconds)
    {
        if (expirationMilliseconds.HasValue && expirationMilliseconds.Value <= 0)
        {
            throw new InvalidOperationException("ExpirationMilliseconds должен быть больше 0.");
        }
    }
}
