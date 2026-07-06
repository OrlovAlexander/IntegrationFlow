using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Exceptions;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

internal static class RabbitMqPublisherConfirms
{
    internal static void EnsureConfirmed(IModel channel, bool publisherConfirmsEnabled, TimeSpan timeout)
    {
        if (!publisherConfirmsEnabled)
        {
            return;
        }

        if (!channel.WaitForConfirms(timeout))
        {
            throw new PublishNotConfirmedException(
                $"RabbitMQ broker did not confirm publish within {timeout.TotalSeconds:0} seconds.");
        }
    }
}
