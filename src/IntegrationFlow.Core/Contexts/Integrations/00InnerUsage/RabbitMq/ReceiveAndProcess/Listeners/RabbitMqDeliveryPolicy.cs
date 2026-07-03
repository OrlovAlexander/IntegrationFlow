using System;
using System.Collections;
using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners
{
    /// <summary>
    /// Правила ack/nack/requeue для RabbitMQ consumer.
    /// </summary>
    internal static class RabbitMqDeliveryPolicy
    {
        internal static bool ShouldRequeue(RabbitMqConfiguration configuration, IDictionary<string, object> headers)
        {
            if (configuration == null)
            {
                return false;
            }

            if (configuration.MaxRetryCount > 0)
            {
                var deathCount = GetDeathCount(headers);
                if (deathCount >= configuration.MaxRetryCount)
                {
                    return false;
                }
            }

            return configuration.RequeueOnFailure;
        }

        internal static int GetDeathCount(IDictionary<string, object> headers)
        {
            if (headers == null || !headers.TryGetValue("x-death", out var deathHeader))
            {
                return 0;
            }

            if (deathHeader is IList deathList && deathList.Count > 0 && deathList[0] is IDictionary deathEntry)
            {
                if (deathEntry.Contains("count"))
                {
                    return Convert.ToInt32(Convert.ToInt64(deathEntry["count"]));
                }
            }

            return 0;
        }
    }
}
