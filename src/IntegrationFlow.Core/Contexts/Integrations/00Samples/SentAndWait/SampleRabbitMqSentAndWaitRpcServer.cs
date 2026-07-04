using System;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;

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
        {
            var configuration = RabbitMqRequestReplyConfigurationLoader.LoadProfile(configurationProfileName);
            var replyPublisher = new RabbitMqReplyPublisher(configuration);
            return message => HandleAsync(message, replyPublisher);
        }

        private static Task HandleAsync(object message, RabbitMqReplyPublisher replyPublisher)
        {
            if (message is not RabbitMqReceivedMessage receivedMessage || !receivedMessage.IsRequestReply)
            {
                return Task.CompletedTask;
            }

            var responseText = BuildResponse(receivedMessage);
            replyPublisher.PublishTextReply(receivedMessage, responseText);
            return Task.CompletedTask;
        }

        private static string BuildResponse(RabbitMqReceivedMessage request)
            => $$"""{"status":"ok","echo":{{request.BodyText}}}""";
    }
}
