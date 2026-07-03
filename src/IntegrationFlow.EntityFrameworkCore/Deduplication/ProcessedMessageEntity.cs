namespace IntegrationFlow.EntityFrameworkCore.Deduplication;

/// <summary>
/// Состояние dedup-записи.
/// </summary>
public enum ProcessedMessageState
{
    Processing = 0,
    Processed = 1
}

/// <summary>
/// EF-сущность dedup store.
/// </summary>
public sealed class ProcessedMessageEntity
{
    public string MessageId { get; set; } = string.Empty;

    public ProcessedMessageState State { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
