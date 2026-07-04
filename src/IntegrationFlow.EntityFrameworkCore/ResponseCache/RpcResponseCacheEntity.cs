namespace IntegrationFlow.EntityFrameworkCore.ResponseCache;

using System;

/// <summary>
/// Состояние кешированного RPC-ответа.
/// </summary>
public enum RpcResponseCacheState
{
    Processing = 0,
    Completed = 1
}

/// <summary>
/// EF-сущность кеша RPC-ответов.
/// </summary>
public sealed class RpcResponseCacheEntity
{
    public string MessageId { get; set; } = string.Empty;

    public RpcResponseCacheState State { get; set; }

    public byte[] ResponseBody { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
