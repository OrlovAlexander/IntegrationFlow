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

        public OutboxMessageStatus Status { get; }

        public string LockedBy { get; }

        public DateTimeOffset? LockedUntil { get; }

        public DateTimeOffset? RetryAfter { get; }

        public string LastError { get; }

        public OutboxMessage(
            Guid id,
            string profileName,
            byte[] payload,
            string contentType,
            DateTimeOffset createdAt,
            int attemptCount)
            : this(
                id,
                profileName,
                payload,
                contentType,
                createdAt,
                attemptCount,
                OutboxMessageStatus.Pending,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: null,
                lastError: null)
        {
        }

        public OutboxMessage(
            Guid id,
            string profileName,
            byte[] payload,
            string contentType,
            DateTimeOffset createdAt,
            int attemptCount,
            OutboxMessageStatus status,
            string lockedBy,
            DateTimeOffset? lockedUntil,
            DateTimeOffset? retryAfter,
            string lastError)
        {
            Id = id;
            ProfileName = profileName ?? string.Empty;
            Payload = payload ?? Array.Empty<byte>();
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType;
            CreatedAt = createdAt;
            AttemptCount = attemptCount;
            Status = status;
            LockedBy = lockedBy;
            LockedUntil = lockedUntil;
            RetryAfter = retryAfter;
            LastError = lastError;
        }

        internal OutboxMessage WithClaim(string workerId, DateTimeOffset lockedUntil)
            => new(
                Id,
                ProfileName,
                Payload,
                ContentType,
                CreatedAt,
                AttemptCount,
                OutboxMessageStatus.InFlight,
                workerId,
                lockedUntil,
                RetryAfter,
                LastError);

        internal OutboxMessage WithPublished()
            => new(
                Id,
                ProfileName,
                Payload,
                ContentType,
                CreatedAt,
                AttemptCount,
                OutboxMessageStatus.Published,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: null,
                lastError: null);

        internal OutboxMessage WithPendingRetry(int attemptCount, DateTimeOffset retryAfter, string error)
            => new(
                Id,
                ProfileName,
                Payload,
                ContentType,
                CreatedAt,
                attemptCount,
                OutboxMessageStatus.Pending,
                lockedBy: null,
                lockedUntil: null,
                retryAfter,
                error);

        internal OutboxMessage WithFailedPermanently(int attemptCount, string error)
            => new(
                Id,
                ProfileName,
                Payload,
                ContentType,
                CreatedAt,
                attemptCount,
                OutboxMessageStatus.Failed,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: null,
                error);

        internal OutboxMessage WithReleasedClaim()
            => new(
                Id,
                ProfileName,
                Payload,
                ContentType,
                CreatedAt,
                AttemptCount,
                OutboxMessageStatus.Pending,
                lockedBy: null,
                lockedUntil: null,
                RetryAfter,
                LastError);

        internal OutboxMessage WithReplay(bool resetAttemptCount)
            => new(
                Id,
                ProfileName,
                Payload,
                ContentType,
                CreatedAt,
                resetAttemptCount ? 0 : AttemptCount,
                OutboxMessageStatus.Pending,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: null,
                lastError: null);
    }
}
