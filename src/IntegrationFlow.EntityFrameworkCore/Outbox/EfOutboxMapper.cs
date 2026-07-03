using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

namespace IntegrationFlow.EntityFrameworkCore.Outbox;

internal static class EfOutboxMapper
{
    internal static OutboxMessageEntity ToEntity(OutboxMessage message)
        => new()
        {
            Id = message.Id,
            ProfileName = message.ProfileName,
            Payload = message.Payload,
            ContentType = message.ContentType,
            CreatedAt = message.CreatedAt,
            AttemptCount = message.AttemptCount,
            Status = message.Status,
            LockedBy = message.LockedBy,
            LockedUntil = message.LockedUntil,
            RetryAfter = message.RetryAfter,
            LastError = message.LastError
        };

    internal static OutboxMessage ToDomain(OutboxMessageEntity entity)
        => new(
            entity.Id,
            entity.ProfileName,
            entity.Payload,
            entity.ContentType,
            entity.CreatedAt,
            entity.AttemptCount,
            entity.Status,
            entity.LockedBy ?? string.Empty,
            entity.LockedUntil,
            entity.RetryAfter,
            entity.LastError ?? string.Empty);
}
