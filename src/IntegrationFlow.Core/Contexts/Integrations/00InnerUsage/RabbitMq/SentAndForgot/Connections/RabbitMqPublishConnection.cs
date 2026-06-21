using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;
using RabbitMQ.Client;
using DomainConnection = IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection.IConnection;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections
{
    /// <summary>
    /// Подключение к RabbitMQ для публикации сообщений.
    /// </summary>
    internal sealed class RabbitMqPublishConnection : DomainConnection
    {
        private RabbitMQ.Client.IConnection connection;
        private IModel channel;
        private readonly RabbitMqPublishConfiguration configuration;
        private bool disposed;

        public RabbitMqPublishConnection(RabbitMqPublishConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Open();
        }

        internal IModel Channel => channel;

        public bool NeedReconnect()
        {
            return connection == null || !connection.IsOpen || channel == null || !channel.IsOpen;
        }

        public bool Reconnect()
        {
            DisposeInternal();
            Open();
            return !NeedReconnect();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeInternal();
        }

        private void Open()
        {
            var factory = RabbitMqConnectionFactory.Create(configuration.ToConnectionSettings());
            connection = factory.CreateConnection();
            channel = connection.CreateModel();
        }

        private void DisposeInternal()
        {
            try
            {
                channel?.Close();
            }
            catch
            {
            }
            finally
            {
                channel?.Dispose();
                channel = null;
            }

            try
            {
                connection?.Close();
            }
            catch
            {
            }
            finally
            {
                connection?.Dispose();
                connection = null;
            }
        }
    }
}
