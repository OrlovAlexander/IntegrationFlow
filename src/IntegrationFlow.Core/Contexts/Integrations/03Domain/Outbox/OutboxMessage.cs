using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Сообщение transactional outbox для последующей публикации в брокер.
    /// </summary>
    public sealed class OutboxMessage
    {
        public Guid Id { get; }

        public string ProfileName { get; }

        public byte[] Payload { get; }

        public string ContentType { get; }

        public DateTimeOffset CreatedAt { get; }

        public int AttemptCount { get; }

        public OutboxMessage(
            Guid id,
            string profileName,
            byte[] payload,
            string contentType,
            DateTimeOffset createdAt,
            int attemptCount)
        {
            Id = id;
            ProfileName = profileName ?? string.Empty;
            Payload = payload ?? Array.Empty<byte>();
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType;
            CreatedAt = createdAt;
            AttemptCount = attemptCount;
        }
    }
}
