using System;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending;

/// <summary>
/// EF-сущность async RPC pending request.
/// </summary>
public sealed class RpcPendingRequestEntity
{
    public Guid Id { get; set; }

    public string ProfileName { get; set; } = string.Empty;

    public byte[] RequestPayload { get; set; } = Array.Empty<byte>();

    public byte[]? ResponsePayload { get; set; }

    public string ContentType { get; set; } = "application/json";

    public DateTimeOffset CreatedAt { get; set; }

    public int AttemptCount { get; set; }

    public RpcPendingStatus Status { get; set; }

    public string? LockedBy { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? RetryAfter { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CompensatedAt { get; set; }

    public string? LastError { get; set; }
}
