using System;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Exceptions
{
    /// <summary>
    /// Брокер не подтвердил приём опубликованного сообщения.
    /// </summary>
    public sealed class PublishNotConfirmedException : Exception
    {
        public PublishNotConfirmedException()
            : base("RabbitMQ broker did not confirm message publish.")
        {
        }

        public PublishNotConfirmedException(string message)
            : base(message)
        {
        }
    }
}
