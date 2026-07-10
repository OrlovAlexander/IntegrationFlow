using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.RpcPending;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

/// <summary>
/// Resolves RPC pending relay transport from REST or RabbitMQ request-reply configuration.
/// </summary>
public sealed class RpcPendingTransportResolver : IRpcPendingTransportResolver
{
    public IRpcPendingPublisher CreatePublisher(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("RPC pending profile name is required.", nameof(profileName));
        }

        if (RestRequestReplyConfigurationLoader.TryLoadProfile(profileName, out var restConfiguration))
        {
            if (restConfiguration.RequestMode != RestRequestReplyRequestMode.AsyncOutbox)
            {
                throw new InvalidOperationException(
                    $"REST profile '{profileName}' must use RequestMode=AsyncOutbox for rpc pending relay.");
            }

            var webhookConfiguration = RestWebhookConfigurationLoader.LoadProfile(
                restConfiguration.ResponseWebhookProfileName);
            return new RestRpcPendingPublisher(restConfiguration, webhookConfiguration);
        }

        try
        {
            var rabbitConfiguration = RabbitMqRequestReplyConfigurationLoader.LoadProfile(profileName);
            return new RabbitMqRpcPendingPublisher(rabbitConfiguration);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"RPC pending profile '{profileName}' was not found in RestRequestReply or RabbitMqRequestReply configuration.",
                ex);
        }
    }
}
