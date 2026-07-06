#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._02Application;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationFlow.Contexts.Integrations._00Samples.SentAndWait
{
    /// <summary>
    /// End-to-end sample: hosted RPC server + SentAndWait client.
    /// </summary>
    internal static class SampleHostedRabbitMqSentAndWaitRpcApplication
    {
        /// <summary>
        /// Имя профиля в секции <c>RabbitMqRequestReply</c> (rabbitmq.json).
        /// </summary>
        public const string RequestReplyProfileName = "OrdersRpc";

        /// <summary>
        /// Регистрирует hosted RPC server. Запуск: <c>await host.RunAsync()</c>.
        /// </summary>
        public static IServiceCollection ConfigureRpcServerHost(IServiceCollection services)
        {
            services.AddIntegrationFlow();
            services.AddIntegrationFlowRabbitMqRpcServer(RequestReplyProfileName);
            return services;
        }

        /// <summary>
        /// Пример RPC-вызова из client-процесса (нужен запущенный server).
        /// </summary>
        public static async Task RunClientExampleAsync(
            IOrgIntegration orgIntegration,
            CancellationToken cancellationToken = default)
        {
            SentAndWaitIntegrationOptions.ThrowOnFailure = true;

            var integration = orgIntegration.CreateSentAndWaitIntegration<SampleRabbitMqSentAndWaitProvider>(
                RequestReplyProfileName,
                new { OrderId = 42 });

            var handler = orgIntegration.GetSentAndWaitResultHandler<SampleRabbitMqSentAndWaitProvider>(
                RequestReplyProfileName);

            await integration.IntegrateAsync(handler, cancellationToken).ConfigureAwait(false);
        }
    }
}
#endif
