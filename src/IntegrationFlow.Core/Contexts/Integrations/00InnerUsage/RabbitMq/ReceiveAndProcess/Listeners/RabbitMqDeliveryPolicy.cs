using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners
{
    /// <summary>
    /// Правила ack/nack/requeue для RabbitMQ consumer.
    /// </summary>
    internal static class RabbitMqDeliveryPolicy
    {
        internal static bool ShouldRequeue(RabbitMqConfiguration configuration, IReadOnlyDictionary<string, object> headers)
        {
            if (configuration == null)
            {
                return false;
            }

            if (configuration.MaxRetryCount > 0)
            {
                var deathCount = RabbitMqMessageHeaders.GetDeathCount(headers);
                if (deathCount >= configuration.MaxRetryCount)
                {
                    return false;
                }
            }

            return configuration.RequeueOnFailure;
        }
    }
}
