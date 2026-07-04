using System;
using System.Collections.Concurrent;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply
{
    /// <summary>
    /// Пул переиспользуемых publish-channel для RPC-ответов по профилю.
    /// </summary>
    internal static class RabbitMqReplyPublisherPool
    {
        private static readonly ConcurrentDictionary<string, RabbitMqReplyPublisherChannel> Channels =
            new(StringComparer.OrdinalIgnoreCase);

        public static RabbitMqReplyPublisherChannel GetOrAdd(RabbitMqRequestReplyConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var key = BuildKey(configuration);
            while (true)
            {
                var channel = Channels.GetOrAdd(
                    key,
                    _ => new RabbitMqReplyPublisherChannel(configuration));

                if (!channel.NeedReconnect())
                {
                    return channel;
                }

                if (Channels.TryRemove(key, out var stale) && ReferenceEquals(stale, channel))
                {
                    stale.Dispose();
                }
            }
        }

        public static void Invalidate(RabbitMqRequestReplyConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (Channels.TryRemove(BuildKey(configuration), out var stale))
            {
                stale.Dispose();
            }
        }

        private static string BuildKey(RabbitMqRequestReplyConfiguration configuration)
            => $"{configuration.HostName}:{configuration.Port}:{configuration.VirtualHost}:{configuration.Name}:reply";
    }
}
