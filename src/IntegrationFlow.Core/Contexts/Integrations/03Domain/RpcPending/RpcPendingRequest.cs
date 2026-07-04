using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Async RPC-запрос, staged в TX приложения и ожидающий ответ через response queue.
    /// </summary>
    public sealed class RpcPendingRequest
    {
        public Guid Id { get; }

        public string ProfileName { get; }

        public byte[] RequestPayload { get; }

        public string ContentType { get; }

        public DateTimeOffset CreatedAt { get; }

        public int AttemptCount { get; }

        public RpcPendingStatus Status { get; }

        public byte[]? ResponsePayload { get; }

        public string LockedBy { get; }

        public DateTimeOffset? LockedUntil { get; }

        public DateTimeOffset? RetryAfter { get; }

        public DateTimeOffset? CompletedAt { get; }

        public DateTimeOffset? CompensatedAt { get; }

        public string LastError { get; }

        public RpcPendingRequest(
            Guid id,
            string profileName,
            byte[] requestPayload,
            string contentType,
            DateTimeOffset createdAt,
            int attemptCount = 0)
            : this(
                id,
                profileName,
                requestPayload,
                contentType,
                createdAt,
                attemptCount,
                RpcPendingStatus.Pending,
                responsePayload: null,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: null,
                completedAt: null,
                lastError: null,
                compensatedAt: null)
        {
        }

        public RpcPendingRequest(
            Guid id,
            string profileName,
            byte[] requestPayload,
            string contentType,
            DateTimeOffset createdAt,
            int attemptCount,
            RpcPendingStatus status,
            byte[]? responsePayload,
            string? lockedBy,
            DateTimeOffset? lockedUntil,
            DateTimeOffset? retryAfter,
            DateTimeOffset? completedAt,
            string? lastError,
            DateTimeOffset? compensatedAt = null)
        {
            Id = id;
            ProfileName = profileName ?? string.Empty;
            RequestPayload = requestPayload ?? Array.Empty<byte>();
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType;
            CreatedAt = createdAt;
            AttemptCount = attemptCount;
            Status = status;
            ResponsePayload = responsePayload;
            LockedBy = lockedBy ?? string.Empty;
            LockedUntil = lockedUntil;
            RetryAfter = retryAfter;
            CompletedAt = completedAt;
            CompensatedAt = compensatedAt;
            LastError = lastError ?? string.Empty;
        }

        internal RpcPendingRequest WithCompensated(DateTimeOffset compensatedAt)
            => Copy(compensatedAt: compensatedAt);

        internal RpcPendingRequest WithClaim(string workerId, DateTimeOffset lockedUntil)
            => Copy(status: RpcPendingStatus.InFlight, lockedBy: workerId, lockedUntil: lockedUntil);

        internal RpcPendingRequest WithAwaitingResponse()
            => Copy(
                status: RpcPendingStatus.AwaitingResponse,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: null,
                lastError: null);

        internal RpcPendingRequest WithCompleted(byte[] responsePayload, DateTimeOffset completedAt)
            => Copy(
                status: RpcPendingStatus.Completed,
                responsePayload: responsePayload,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: null,
                completedAt: completedAt,
                lastError: null);

        internal RpcPendingRequest WithPendingRetry(int attemptCount, DateTimeOffset retryAfter, string error)
            => Copy(
                status: RpcPendingStatus.Pending,
                attemptCount: attemptCount,
                lockedBy: null,
                lockedUntil: null,
                retryAfter: retryAfter,
                lastError: error);

        internal RpcPendingRequest WithFailed(int attemptCount, string error)
            => Copy(
                status: RpcPendingStatus.Failed,
                attemptCount: attemptCount,
                lockedBy: null,
                lockedUntil: null,
                lastError: error);

        internal RpcPendingRequest WithTimedOut(string error)
            => Copy(
                status: RpcPendingStatus.TimedOut,
                lockedBy: null,
                lockedUntil: null,
                lastError: error);

        internal RpcPendingRequest WithReleasedClaim()
            => Copy(status: RpcPendingStatus.Pending, lockedBy: null, lockedUntil: null);

        private RpcPendingRequest Copy(
            RpcPendingStatus? status = null,
            int? attemptCount = null,
            byte[]? responsePayload = null,
            string? lockedBy = null,
            DateTimeOffset? lockedUntil = null,
            DateTimeOffset? retryAfter = null,
            DateTimeOffset? completedAt = null,
            string? lastError = null,
            DateTimeOffset? compensatedAt = null)
            => new(
                Id,
                ProfileName,
                RequestPayload,
                ContentType,
                CreatedAt,
                attemptCount ?? AttemptCount,
                status ?? Status,
                responsePayload ?? ResponsePayload,
                lockedBy ?? LockedBy,
                lockedUntil ?? LockedUntil,
                retryAfter ?? RetryAfter,
                completedAt ?? CompletedAt,
                lastError ?? LastError,
                compensatedAt ?? CompensatedAt);
    }
}
