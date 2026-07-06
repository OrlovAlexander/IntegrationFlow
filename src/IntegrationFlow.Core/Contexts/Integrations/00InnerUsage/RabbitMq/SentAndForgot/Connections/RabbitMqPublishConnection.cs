using System;
using System.Threading;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Exceptions;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DomainConnection = IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection.IConnection;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections
{
    /// <summary>
    /// Подключение к RabbitMQ для публикации сообщений.
    /// </summary>
    internal sealed class RabbitMqPublishConnection : DomainConnection, ILeaveOpenOnDispose
    {
        private RabbitMQ.Client.IConnection connection;
        private IModel channel;
        private readonly RabbitMqPublishConfiguration configuration;
        private bool disposed;
        private volatile bool unroutableMessageReceived;
        private readonly ManualResetEventSlim unroutableSignal = new(initialState: false);

        public RabbitMqPublishConnection(RabbitMqPublishConfiguration configuration)
            : this(configuration, leaveOpenOnDispose: false, openConnection: true)
        {
        }

        internal RabbitMqPublishConnection(RabbitMqPublishConfiguration configuration, bool leaveOpenOnDispose)
            : this(configuration, leaveOpenOnDispose, openConnection: true)
        {
        }

        internal RabbitMqPublishConnection(
            RabbitMqPublishConfiguration configuration,
            bool leaveOpenOnDispose,
            bool openConnection)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            LeaveOpenOnDispose = leaveOpenOnDispose;
            if (openConnection)
            {
                Open();
            }
        }

        public bool LeaveOpenOnDispose { get; }

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
            if (disposed || LeaveOpenOnDispose)
            {
                return;
            }

            disposed = true;
            DisposeInternal();
        }

        internal void ForceDispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeInternal();
        }

        internal void ResetUnroutableFlag()
        {
            unroutableMessageReceived = false;
            unroutableSignal.Reset();
        }

        internal void WaitForUnroutableProcessing(TimeSpan timeout)
        {
            if (!configuration.Mandatory)
            {
                return;
            }

            unroutableSignal.Wait(timeout);
        }

        internal void EnsureNotUnroutable()
        {
            if (configuration.Mandatory && unroutableMessageReceived)
            {
                throw new UnroutableMessageException("RabbitMQ returned BasicReturn for mandatory publish.");
            }
        }

        internal void SimulateBasicReturnForTesting()
        {
            OnBasicReturn(this, null!);
        }

        private void Open()
        {
            var factory = RabbitMqConnectionFactory.Create(configuration.ToConnectionSettings());
            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            if (configuration.PublisherConfirmsEnabled)
            {
                channel.ConfirmSelect();
            }

            if (configuration.Mandatory)
            {
                channel.BasicReturn += OnBasicReturn;
            }
        }

        private void OnBasicReturn(object sender, BasicReturnEventArgs eventArgs)
        {
            if (!configuration.Mandatory)
            {
                return;
            }

            unroutableMessageReceived = true;
            unroutableSignal.Set();
        }

        private void DisposeInternal()
        {
            if (channel != null && configuration.Mandatory)
            {
                channel.BasicReturn -= OnBasicReturn;
            }

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
