using System;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
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
            var configuration = RabbitMqRequestReplyConfigurationLoader.LoadProfile(configurationProfileName);
            var replyPublisher = new RabbitMqReplyPublisher(configuration);
            return message => HandleAsync(message, replyPublisher, responseStore);
        }

        private static Task HandleAsync(
            object message,
            RabbitMqReplyPublisher replyPublisher,
            IRequestReplyResponseStore? responseStore)
        {
            if (message is not RabbitMqReceivedMessage receivedMessage || !receivedMessage.IsRequestReply)
            {
                return Task.CompletedTask;
            }

            return RabbitMqRpcServerPipeline.HandleAsync(
                receivedMessage,
                replyPublisher,
                BuildResponseAsync,
                responseStore);
        }

        private static Task<string> BuildResponseAsync(RabbitMqReceivedMessage request)
            => Task.FromResult($$"""{"status":"ok","echo":{{request.BodyText}}}""");
    }
}
