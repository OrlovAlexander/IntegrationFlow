using System;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;

namespace IntegrationFlow.Contexts.Integrations._00Samples.SentAndWait
{
    /// <summary>
    /// Пример server-side обработчика RPC-запросов: reply публикуется до return из handler.
    /// </summary>
    internal static class SampleRabbitMqSentAndWaitRpcServer
    {
        /// <summary>
        /// Создаёт hosted-listener handler для профиля request-reply.
        /// </summary>
        public static Func<object, Task> CreateHandler(string configurationProfileName)
            => CreateHandler(configurationProfileName, responseStore: null);

        /// <summary>
        /// Создаёт handler с опциональным кешем ответов для идемпотентного RPC.
        /// </summary>
        public static Func<object, Task> CreateHandler(
            string configurationProfileName,
            IRequestReplyResponseStore? responseStore)
        {
            var processing = new RabbitMqRpcServerInboxMessageProcessing(configurationProfileName, responseStore: responseStore);
            return message =>
            {
                if (message is RabbitMqReceivedMessage receivedMessage)
                {
                    processing.ProcessInboxMessage(new IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessage(receivedMessage));
                }

                return Task.CompletedTask;
            };
        }
    }
}
