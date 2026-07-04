using System;
using System.Collections.Concurrent;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections
{
    /// <summary>
    /// Пул переиспользуемых RPC-подключений по профилю конфигурации.
    /// </summary>
    internal static class RabbitMqRequestReplyConnectionPool
    {
        private static readonly ConcurrentDictionary<string, RabbitMqRequestReplyConnection> Connections = new(StringComparer.OrdinalIgnoreCase);

        public static RabbitMqRequestReplyConnection GetOrAdd(RabbitMqRequestReplyConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var key = BuildKey(configuration);
            return Connections.GetOrAdd(
                key,
                _ => new RabbitMqRequestReplyConnection(configuration, leaveOpenOnDispose: true));
        }

        private static string BuildKey(RabbitMqRequestReplyConfiguration configuration)
            => $"{configuration.HostName}:{configuration.Port}:{configuration.VirtualHost}:{configuration.Name}";
    }
}
