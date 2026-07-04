using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending;

internal static class EfRpcPendingMapper
{
    internal static RpcPendingRequestEntity ToEntity(RpcPendingRequest request)
        => new()
        {
            Id = request.Id,
            ProfileName = request.ProfileName,
            RequestPayload = request.RequestPayload,
            ResponsePayload = request.ResponsePayload,
            ContentType = request.ContentType,
            CreatedAt = request.CreatedAt,
            AttemptCount = request.AttemptCount,
            Status = request.Status,
            LockedBy = request.LockedBy,
            LockedUntil = request.LockedUntil,
            RetryAfter = request.RetryAfter,
            CompletedAt = request.CompletedAt,
            LastError = request.LastError
        };

    internal static RpcPendingRequest ToDomain(RpcPendingRequestEntity entity)
        => new(
            entity.Id,
            entity.ProfileName,
            entity.RequestPayload,
            entity.ContentType,
            entity.CreatedAt,
            entity.AttemptCount,
            entity.Status,
            entity.ResponsePayload,
            entity.LockedBy,
            entity.LockedUntil,
            entity.RetryAfter,
            entity.CompletedAt,
            entity.LastError);
}
