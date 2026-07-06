using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners
{
    /// <summary>
    /// Асинхронный потокобезопасный слушатель очереди RabbitMQ с ручным подтверждением получения сообщений.
    /// </summary>
    internal sealed class RabbitMqListener : ListenerBase
    {
        private readonly RabbitMqListenerWorker worker = new();
        private volatile bool listening;

        /// <inheritdoc />
        protected override Task RunAsync(CancellationToken cancellationToken, Action? postStartAction)
        {
            var configuration = (RabbitMqConfiguration)Configuration;

            return worker.RunAsync(
                configuration,
                message => ProcessMessageAsync(message, cancellationToken),
                Logger,
                cancellationToken,
                () =>
                {
                    listening = true;
                    postStartAction?.Invoke();
                },
                () => listening = false,
                Publisher.Metrics);
        }

        /// <inheritdoc />
        protected override void DisposeInternal(bool disposing)
        {
            listening = false;
        }

        /// <inheritdoc />
        protected override ListenerStatuses GetStatusInternal(ListenerStatuses listenerStatuses)
        {
            return listening ? ListenerStatuses.Started : ListenerStatuses.NotStarted;
        }
    }
}
