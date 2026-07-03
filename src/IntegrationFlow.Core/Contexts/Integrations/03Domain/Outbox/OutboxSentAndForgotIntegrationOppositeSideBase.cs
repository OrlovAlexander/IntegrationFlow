using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Противоположная сторона SentAndForgot, записывающая сообщения в transactional outbox.
    /// </summary>
    internal abstract class OutboxSentAndForgotIntegrationOppositeSideBase : SentAndForgotIntegrationOppositeSide
    {
        private readonly IOutboxStore outboxStore;
        private readonly IOutboxEnqueue outboxEnqueue;

        protected OutboxSentAndForgotIntegrationOppositeSideBase(IOutboxStore outboxStore)
            : this(outboxStore, null)
        {
        }

        protected OutboxSentAndForgotIntegrationOppositeSideBase(IOutboxStore outboxStore, IOutboxEnqueue outboxEnqueue)
        {
            this.outboxStore = outboxStore;
            this.outboxEnqueue = outboxEnqueue;
        }

        protected OutboxSentAndForgotIntegrationOppositeSideBase(IOutboxEnqueue outboxEnqueue)
        {
            this.outboxEnqueue = outboxEnqueue;
        }

        protected abstract string ProfileName { get; }

        protected virtual string ContentType => "application/json";

        public override IFormatterTransmitData GetFormatterSourceData(IIntegrationLogger logger) => null;

        public override IConfiguration GetTransmitterConfiguration(IIntegrationLogger logger) => null;

        public override IConnection GetConnection(IConfiguration configuration, IIntegrationLogger logger)
            => NullSentAndForgotConnection.Instance;

        public override ITransmitter GetTransmitter(IConfiguration configuration, IConnection connection, IIntegrationLogger logger)
        {
            if (outboxEnqueue != null)
            {
                return new OutboxTransmitter(outboxEnqueue, ProfileName, ContentType);
            }

            return new OutboxTransmitter(outboxStore, ProfileName, ContentType);
        }

        public override ILogging GetLogging(IIntegrationLogger logger) => null;

        /// <inheritdoc />
        protected override object GetIntegrationOppositeSideCode() => ProfileName;
    }
}
