using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;

namespace IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess
{
    /// <summary>
    /// Пример обработчика, читающего AMQP headers из входящего сообщения.
    /// </summary>
    internal static class SampleRabbitMqHeaderAwareReceiveAndProcessHandler
    {
        /// <summary>
        /// Имя custom header для маршрутизации по tenant (пример).
        /// </summary>
        public const string TenantHeaderName = "x-tenant-id";

        /// <summary>
        /// Обрабатывает сообщение с учётом tenant и distributed tracing headers.
        /// </summary>
        public static Task HandleAsync(object message)
        {
            if (message is not RabbitMqReceivedMessage receivedMessage)
            {
                return Task.CompletedTask;
            }

            if (RabbitMqMessageHeaders.TryGetString(receivedMessage.Headers, TenantHeaderName, out var tenantId))
            {
                // tenant-specific routing / authorization
                _ = tenantId;
            }

            if (RabbitMqMessageHeaders.TryGetString(receivedMessage.Headers, RabbitMqMessageHeaders.TraceParent, out var traceParent))
            {
                // correlate with upstream trace (Activity уже создан каркасом при consume)
                _ = traceParent;
            }

            var deathCount = RabbitMqMessageHeaders.GetDeathCount(receivedMessage.Headers);
            if (deathCount > 0)
            {
                // custom retry / alerting logic before default nack/requeue policy
                _ = deathCount;
            }

            return Task.CompletedTask;
        }
    }
}
