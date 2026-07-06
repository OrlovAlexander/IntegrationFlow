using System;
using System.Collections.Concurrent;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections
{
    /// <summary>
    /// Пул переиспользуемых RPC-подключений по профилю конфигурации.
    /// </summary>
    internal static class RabbitMqRequestReplyConnectionPool
    {
        private static readonly ConcurrentDictionary<string, RabbitMqRequestReplyConnection> Connections = new(StringComparer.OrdinalIgnoreCase);
        private static IIntegrationFlowMetrics? metrics;

        static RabbitMqRequestReplyConnectionPool()
        {
            RabbitMqConnectionPoolRegistry.Register(DisposeAll);
        }

        internal static void SetMetrics(IIntegrationFlowMetrics? integrationFlowMetrics)
            => metrics = integrationFlowMetrics;

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
                    RecordPoolSize();
                    return connection;
                }

                if (connection.Reconnect())
                {
                    RecordPoolSize();
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

            RecordPoolSize();
        }

        internal static void DisposeAll()
        {
            foreach (var key in Connections.Keys.ToArray())
            {
                if (Connections.TryRemove(key, out var connection))
                {
                    connection.ForceDispose();
                }
            }

            RecordPoolSize();
        }

        internal static int Count => Connections.Count;

        private static void RecordPoolSize()
            => metrics?.RecordConnectionPoolSize("rpc", Connections.Count);

        private static string BuildKey(RabbitMqRequestReplyConfiguration configuration)
            => $"{configuration.HostName}:{configuration.Port}:{configuration.VirtualHost}:{configuration.Name}";
    }
}
