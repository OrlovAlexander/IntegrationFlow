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
            while (true)
            {
                var connection = Connections.GetOrAdd(
                    key,
                    _ => new RabbitMqRequestReplyConnection(configuration, leaveOpenOnDispose: true));

                if (!connection.NeedReconnect())
                {
                    return connection;
                }

                if (connection.Reconnect())
                {
                    return connection;
                }

                if (Connections.TryRemove(key, out var stale) && ReferenceEquals(stale, connection))
                {
                    stale.ForceDispose();
                }
            }
        }

        public static void Invalidate(RabbitMqRequestReplyConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (Connections.TryRemove(BuildKey(configuration), out var stale))
            {
                stale.ForceDispose();
            }
        }

        private static string BuildKey(RabbitMqRequestReplyConfiguration configuration)
            => $"{configuration.HostName}:{configuration.Port}:{configuration.VirtualHost}:{configuration.Name}";
    }
}
