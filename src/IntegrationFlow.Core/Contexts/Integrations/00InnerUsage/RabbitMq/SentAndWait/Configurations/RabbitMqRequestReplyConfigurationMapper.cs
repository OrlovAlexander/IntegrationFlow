using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;

/// <summary>
/// Maps <see cref="RabbitMqRequestReplyConfiguration"/> to listener <see cref="RabbitMqConfiguration"/>.
/// </summary>
internal static class RabbitMqRequestReplyConfigurationMapper
{
    internal static RabbitMqConfiguration ToListenerConfiguration(RabbitMqRequestReplyConfiguration source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        source.Validate();

        var prefetch = source.MaxConcurrentRequests > 0
            ? (ushort)Math.Min(ushort.MaxValue, source.MaxConcurrentRequests)
            : (ushort)1;

        return new RabbitMqConfiguration
        {
            Name = source.Name,
            HostName = source.HostName,
            Port = source.Port,
            UserName = source.UserName,
            Password = source.Password,
            VirtualHost = source.VirtualHost,
            QueueName = source.QueueName,
            PrefetchCount = prefetch,
            AutomaticRecoveryEnabled = source.AutomaticRecoveryEnabled,
            ClientProvidedName = ResolveServerClientProvidedName(source),
            SslEnabled = source.SslEnabled,
            SslServerName = source.SslServerName,
        };
    }

    private static string ResolveServerClientProvidedName(RabbitMqRequestReplyConfiguration source)
    {
        if (!string.IsNullOrWhiteSpace(source.ClientProvidedName) &&
            source.ClientProvidedName.IndexOf("RpcServer", StringComparison.OrdinalIgnoreCase) < 0 &&
            source.ClientProvidedName.IndexOf("Client", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return source.ClientProvidedName.Replace("Client", "Server");
        }

        return string.IsNullOrWhiteSpace(source.Name)
            ? "IntegrationFlow.RabbitMqRpcServer"
            : $"IntegrationFlow.RabbitMqRpcServer.{source.Name}";
    }
}
