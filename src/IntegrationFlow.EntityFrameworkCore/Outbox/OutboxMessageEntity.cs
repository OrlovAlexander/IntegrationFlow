using System;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

namespace IntegrationFlow.EntityFrameworkCore.Outbox;

/// <summary>
/// EF-сущность transactional outbox.
/// </summary>
public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }

    public string ProfileName { get; set; } = string.Empty;

    public byte[] Payload { get; set; } = Array.Empty<byte>();

    public string ContentType { get; set; } = "application/json";

    public DateTimeOffset CreatedAt { get; set; }

    public int AttemptCount { get; set; }

    public OutboxMessageStatus Status { get; set; }

    public string? LockedBy { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? RetryAfter { get; set; }

    public string? LastError { get; set; }
}
