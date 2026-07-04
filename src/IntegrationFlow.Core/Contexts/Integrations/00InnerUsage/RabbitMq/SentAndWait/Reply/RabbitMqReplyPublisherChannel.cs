using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply
{
    /// <summary>
    /// Переиспользуемый publish-channel для RPC-ответов.
    /// </summary>
    internal sealed class RabbitMqReplyPublisherChannel : IDisposable
    {
        private readonly RabbitMqRequestReplyConfiguration configuration;
        private IConnection connection;
        private IModel channel;

        public RabbitMqReplyPublisherChannel(RabbitMqRequestReplyConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            EnsureOpen();
        }

        public IModel Channel
        {
            get
            {
                EnsureOpen();
                return channel;
            }
        }

        public bool NeedReconnect()
        {
            return connection == null ||
                   !connection.IsOpen ||
                   channel == null ||
                   !channel.IsOpen;
        }

        public void EnsureOpen()
        {
            if (!NeedReconnect())
            {
                return;
            }

            DisposeChannels();
            var factory = RabbitMqConnectionFactory.Create(configuration.ToConnectionSettings());
            connection = factory.CreateConnection();
            channel = connection.CreateModel();
        }

        public void Dispose()
        {
            DisposeChannels();
        }

        private void DisposeChannels()
        {
            if (channel != null)
            {
                try
                {
                    channel.Close();
                }
                catch
                {
                }
                finally
                {
                    channel.Dispose();
                    channel = null;
                }
            }

            if (connection != null)
            {
                try
                {
                    connection.Close();
                }
                catch
                {
                }
                finally
                {
                    connection.Dispose();
                    connection = null;
                }
            }
        }
    }
}
