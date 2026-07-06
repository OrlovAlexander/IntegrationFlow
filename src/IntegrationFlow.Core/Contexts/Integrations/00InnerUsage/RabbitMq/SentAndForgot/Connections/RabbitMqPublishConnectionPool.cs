using System;
using System.Collections.Concurrent;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;

/// <summary>
/// Pool of reusable publish connections per profile.
/// </summary>
internal static class RabbitMqPublishConnectionPool
{
    private static readonly ConcurrentDictionary<string, RabbitMqPublishConnection> Connections =
        new(StringComparer.OrdinalIgnoreCase);

    private static IIntegrationFlowMetrics? metrics;

    static RabbitMqPublishConnectionPool()
    {
        RabbitMqConnectionPoolRegistry.Register(DisposeAll);
    }

    internal static void SetMetrics(IIntegrationFlowMetrics? integrationFlowMetrics)
        => metrics = integrationFlowMetrics;

    public static RabbitMqPublishConnection GetOrAdd(RabbitMqPublishConfiguration configuration)
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
                _ => new RabbitMqPublishConnection(configuration, leaveOpenOnDispose: true));

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

    public static void Invalidate(RabbitMqPublishConfiguration configuration)
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
        => metrics?.RecordConnectionPoolSize("publish", Connections.Count);

    private static string BuildKey(RabbitMqPublishConfiguration configuration)
        => $"{configuration.HostName}:{configuration.Port}:{configuration.VirtualHost}:{configuration.Name}";
}
